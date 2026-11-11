using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Imports;
using Averio.Domain.Common;
using Averio.Domain.Transfers;

namespace Averio.Infrastructure.Imports;

/// <summary>
/// Reads the YNAB web app's transaction-register export, not a bank CSV or
/// YNAB's category-budget report. No external service receives the file.
/// </summary>
public sealed class YnabRegisterReader : IYnabImportReader
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRows = 10000;
    private const int MaxExpandedBytes = 32 * 1024 * 1024;
    private static readonly string[] Columns =
        ["Account", "Flag", "Date", "Payee", "Category Group/Category",
         "Category Group", "Category", "Memo", "Outflow", "Inflow", "Cleared"];
    private static readonly Regex TransferPayee = new(
        @"^Transfer\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static bool IsOpeningBalance(string payee)
    {
        var normalized = Regex.Replace(payee.Trim(), @"\s+", " ");
        return normalized.Equals("Starting Balance", StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsBalanceAdjustment(string payee)
        => IsOpeningBalance(payee)
            || Regex.Replace(payee.Trim(), @"\s+", " ").Equals("Manual Balance Adjustment", StringComparison.OrdinalIgnoreCase)
            || Regex.Replace(payee.Trim(), @"\s+", " ").Equals("Reconciliation Balance Adjustment", StringComparison.OrdinalIgnoreCase);
    private sealed record RegisterEntry(int Line, string Account, string? OtherAccount, DateOnly Date,
        string Kind, decimal Amount, decimal SignedAmount, string Category, string Payee, string Note);
    private static BusinessRuleException Invalid() => new("import_invalid", "Error_ImportInvalid");
    private static BusinessRuleException UnsupportedAdjustment() => new("import_ynab_adjustment_unsupported", "Error_ImportInvalid");

    public YnabImportReadResult Read(Stream stream, string fileName, string currency,
        string dateFormat, string numberFormat)
    {
        try
        {
            if (!CurrencyCode.IsSupported(currency)
                || dateFormat is not ("MDY" or "DMY" or "YMD")
                || numberFormat is not ("DotDecimal" or "CommaDecimal")
                || !stream.CanSeek || stream.Length is <= 0 or > MaxBytes)
                throw Invalid();
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext is not (".csv" or ".tsv" or ".zip")) throw Invalid();
            var text = ext == ".zip" ? ReadArchive(stream) : ReadText(stream, MaxBytes);
            var records = Parse(text, ext == ".tsv" ? '\t' : GuessDelimiter(text), currency, dateFormat, numberFormat);
            var rows = Normalize(records, currency);
            var sourceAccounts = BuildSourceAccounts(records, currency);
            if (rows.Count == 0 && sourceAccounts.Count == 0) throw Invalid();
            return new(rows, sourceAccounts);
        }
        catch (Exception e) when (e is InvalidDataException or DecoderFallbackException
            or FormatException or OverflowException or ArgumentException or IOException)
        {
            throw Invalid();
        }
    }

    private static string ReadText(Stream stream, int maxBytes)
    {
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        if (bytes.Length > maxBytes) throw Invalid();
        return new UTF8Encoding(false, true).GetString(bytes.ToArray()).TrimStart('\uFEFF');
    }

    private static string ReadArchive(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        if (archive.Entries.Count is < 1 or > 100 ||
            archive.Entries.Sum(e => e.Length) > MaxExpandedBytes ||
            archive.Entries.Any(e => e.Length > MaxExpandedBytes
                || e.FullName.StartsWith('/') || e.FullName.Contains("..", StringComparison.Ordinal)
                || e.FullName.Contains('\\')))
            throw Invalid();

        var candidates = new List<(string Name, string Content)>();
        foreach (var entry in archive.Entries.Where(e =>
            e.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            || e.FullName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)))
        {
            using var input = entry.Open();
            var body = ReadText(input, MaxExpandedBytes);
            if (LooksLikeRegister(body, entry.Name.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)
                ? '\t' : GuessDelimiter(body)))
                candidates.Add((entry.Name, body));
        }
        if (candidates.Count != 1) throw Invalid();
        return candidates[0].Content;
    }

    private static bool LooksLikeRegister(string text, char separator)
    {
        var firstLine = text.Split('\n', 2)[0].TrimEnd('\r');
        var first = ParseDelimited(firstLine, separator).FirstOrDefault();
        return first is not null && first.SequenceEqual(Columns);
    }

    private static char GuessDelimiter(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        return firstLine.Contains('\t') ? '\t' : ',';
    }

    private static IReadOnlyList<RegisterEntry> Parse(string text, char separator, string currency,
        string dateFormat, string numberFormat)
    {
        var table = ParseDelimited(text, separator);
        if (table.Count < 2 || !table[0].SequenceEqual(Columns) || table.Count - 1 > MaxRows)
            throw Invalid();
        var rows = new List<RegisterEntry>(table.Count - 1);
        string[] formats = dateFormat switch {
            "MDY" => ["M/d/yyyy", "M/d/yyyy", "MM/dd/yyyy", "M-d-yyyy", "MM-dd-yyyy"],
            "DMY" => ["d/M/yyyy", "d.M.yyyy", "d-M-yyyy", "dd/MM/yyyy", "dd.MM.yyyy", "dd-MM-yyyy"],
            _ => ["yyyy-M-d", "yyyy/M/d", "yyyy.M.d"]
        };
        for (var i = 1; i < table.Count; i++)
        {
            var cell = table[i];
            if (cell.Length != Columns.Length) throw Invalid();
            if (cell.All(string.IsNullOrWhiteSpace)) continue;
            string Field(int index, int max)
            {
                var value = cell[index].Trim();
                return value.Length <= max ? value : throw Invalid();
            }
            var account = Field(0, 100);
            var payee = Field(3, 160);
            // Adjustment rows change the YNAB balance without a normal transaction.
            // Until they can be represented explicitly, reject the export instead of
            // silently importing a financially incorrect account history.
            if (IsBalanceAdjustment(payee) && !IsOpeningBalance(payee)) throw UnsupportedAdjustment();
            var group = Field(5, 100);
            var category = Field(6, 100);
            var note = Field(7, 500);
            var flag = Field(1, 40);
            var cleared = Field(10, 40);
            var dateText = Field(2, 30);
            var dateFormats = dateText.Length >= 4 && dateText[..4].All(char.IsDigit)
                ? new[] { "yyyy-M-d", "yyyy/M/d", "yyyy.M.d", "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd" }
                : formats;
            if (account.Length == 0 || !DateOnly.TryParseExact(dateText, dateFormats,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw Invalid();
            if (category.Equals("Split", StringComparison.OrdinalIgnoreCase)
                || category.StartsWith("Split (", StringComparison.OrdinalIgnoreCase)
                || cell[4].Trim().Equals("Split", StringComparison.OrdinalIgnoreCase)
                || cell[4].Trim().StartsWith("Split (", StringComparison.OrdinalIgnoreCase))
                throw Invalid(); // Do not double-count split-parent and split-leg rows.
            var outflow = Money(Field(8, 60), currency, numberFormat);
            var inflow = Money(Field(9, 60), currency, numberFormat);
            if ((outflow == 0 && inflow == 0 && !IsOpeningBalance(payee))
                || (outflow != 0 && inflow != 0))
                throw Invalid();
            var signed = inflow - outflow;
            var kind = signed > 0 ? "Income" : "Expense";
            var amount = decimal.Abs(signed);
            if ((amount != 0 && !Transfer.IsValidAmount(amount)) || (amount == 0 && !IsOpeningBalance(payee))) throw Invalid();
            var transfer = TransferPayee.Match(payee);
            var other = transfer.Success ? transfer.Groups[1].Value.Trim() : null;
            if (other is not null && (other.Length is 0 or > 100 || other == account))
                throw Invalid();
            var sourceCategory = group.Length > 0 ? group + " / " + category
                : category.Length > 0 ? category : Field(4, 100);
            if (sourceCategory.Length > 100) throw Invalid();
            if (other is null && sourceCategory.Length == 0)
                sourceCategory = kind == "Income" ? "Uncategorized income" : "Uncategorized expense";
            if (flag.Length > 0 || cleared.Length > 0)
            {
                var meta = string.Join("; ", new[] {
                    flag.Length > 0 ? "YNAB flag: " + flag : "",
                    cleared.Length > 0 ? "YNAB status: " + cleared : ""
                }.Where(x => x.Length > 0));
                note = note.Length > 0 ? note + " | " + meta : meta;
                if (note.Length > 500) throw Invalid();
            }

            rows.Add(new(i + 1, account, other, date, kind, amount, signed, sourceCategory, payee, note));
        }
        if (rows.Count == 0) throw Invalid();
        return rows;
    }

    private static decimal Money(string raw, string currency, string numberFormat)
    {
        if (raw.Length == 0) return 0;
        var value = raw.Trim().Replace("\u00a0", "", StringComparison.Ordinal)
            .Replace("\u202f", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        foreach (var symbol in currency switch {
            "EUR" => new[] { "€", "EUR" },
            "USD" => new[] { "$", "USD" },
            "HUF" => new[] { "Ft", "HUF" },
            _ => Array.Empty<string>()
        })
            value = value.Replace(symbol, "", StringComparison.OrdinalIgnoreCase);
        var negative = value.StartsWith('-');
        if (negative || value.StartsWith('+')) value = value[1..];
        var pattern = numberFormat == "DotDecimal"
            ? @"^(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)(?:\.[0-9]{1,4})?$"
            : @"^(?:[0-9]+|[0-9]{1,3}(?:\.[0-9]{3})+)(?:,[0-9]{1,4})?$";
        if (!Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant)) throw Invalid();
        var culture = CultureInfo.GetCultureInfo(numberFormat == "DotDecimal" ? "en-US" : "de-DE");
        var parsed = decimal.Parse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, culture);
        return negative ? -parsed : parsed;
    }

    private static List<string[]> ParseDelimited(string source, char delimiter)
    {
        if (source.Length > MaxExpandedBytes) throw Invalid();
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closed = false;
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < source.Length && source[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') { quoted = false; closed = true; }
                else field.Append(c);
            }
            else if (c == '"' && field.Length == 0 && !closed) quoted = true;
            else if (c == delimiter || c == '\r' || c == '\n')
            {
                row.Add(field.ToString()); field.Clear(); closed = false;
                if (c != delimiter)
                {
                    rows.Add(row.ToArray()); row.Clear();
                    if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                }
            }
            else if (c == '"' || closed) throw Invalid();
            else field.Append(c);
            if (field.Length > 2000 || rows.Count > MaxRows + 2) throw Invalid();
        }
        if (quoted) throw Invalid();
        if (row.Count > 0 || field.Length > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }

    private static IReadOnlyList<ImportSourceAccount> BuildSourceAccounts(
        IReadOnlyList<RegisterEntry> records, string currency)
    {
        return records.GroupBy(r => r.Account, StringComparer.Ordinal)
            .Select(group =>
            {
                var openings = group.Where(r => IsOpeningBalance(r.Payee)).ToArray();
                if (openings.Length > 1) throw Invalid();
                var opening = openings.SingleOrDefault();
                return new ImportSourceAccount(group.Key, currency,
                    opening?.SignedAmount.ToString(CultureInfo.InvariantCulture), opening?.Date);
            })
            .OrderBy(account => account.Source, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ImportRow> Normalize(IReadOnlyList<RegisterEntry> records, string currency)
    {
        var output = new List<ImportRow>();
        var used = new HashSet<int>();
        for (var i = 0; i < records.Count; i++)
        {
            var r = records[i];
            // Opening balances prefill newly created accounts; they are not ledger activity.
            if (IsOpeningBalance(r.Payee)) continue;
            if (r.OtherAccount is null)
            {
                output.Add(new("YNAB Register", r.Line, r.Kind, r.Date, r.Account, null,
                    r.Category, r.Amount, currency, null, null, r.Payee, r.Note));
                continue;
            }
            if (used.Contains(i)) continue;
            // The source register represents transfers twice. Only a complete opposite
            // leg is accepted; never convert unmatched transfer legs into expenses.
            var matchIndex = -1;
            for (var j = i + 1; j < records.Count; j++)
            {
                var other = records[j];
                if (used.Contains(j) || other.OtherAccount != r.Account ||
                    other.Account != r.OtherAccount || other.Date != r.Date ||
                    other.Amount != r.Amount || other.Kind == r.Kind) continue;
                matchIndex = j; break;
            }
            // Some exports order the incoming leg first. Search both directions.
            if (matchIndex == -1)
                for (var j = 0; j < i; j++)
                {
                    var other = records[j];
                    if (used.Contains(j) || other.OtherAccount != r.Account ||
                        other.Account != r.OtherAccount || other.Date != r.Date ||
                        other.Amount != r.Amount || other.Kind == r.Kind) continue;
                    matchIndex = j; break;
                }
            if (matchIndex < 0) throw Invalid();
            used.Add(i); used.Add(matchIndex);
            var pair = records[matchIndex];
            var outgoing = r.Kind == "Expense" ? r : pair;
            var incoming = r.Kind == "Income" ? r : pair;
            var note = outgoing.Note;
            if (incoming.Note.Length > 0 && incoming.Note != outgoing.Note)
                note = note.Length > 0 ? note + " | " + incoming.Note : incoming.Note;
            if (note.Length > 500) throw Invalid();
            output.Add(new("YNAB Register", outgoing.Line, "Transfer", outgoing.Date,
                outgoing.Account, incoming.Account, "", outgoing.Amount, currency,
                incoming.Amount, currency, "", note));
        }
        return output.OrderBy(r => r.RowNumber).ToArray();
    }
}
