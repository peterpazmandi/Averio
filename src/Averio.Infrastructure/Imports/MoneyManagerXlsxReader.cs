using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Averio.Application.Common.Exceptions;
using Averio.Application.Imports;
using Averio.Domain.Common;
using Averio.Domain.Transfers;

namespace Averio.Infrastructure.Imports;

// Recognizes the verified Hungarian Money manager export, not arbitrary spreadsheets.
public sealed class MoneyManagerXlsxReader : IXlsxImportReader
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRows = 10000;
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly string[] TransactionHeaders = ["Dátum és idő", "Kategória", "Számla",
        "Összeg alapértelmezett pénznemben", "Alapértelmezett pénznem", "Összeg a számla pénznemében",
        "Számla pénzneme", "Tranzakció összege a tranzakció pénznemében", "Tranzakció pénzneme", "Címkék", "Megjegyzés"];
    private static readonly string[] TransferHeaders = ["Dátum és idő", "Kimenő", "Beérkező",
        "Összeg kimenő pénznemben", "Kimenő pénznem", "Összeg bejövő pénznemben", "Bejövő pénznem", "Megjegyzés"];
    private static BusinessRuleException Invalid() => new("import_invalid", "Error_ImportInvalid");
    public IReadOnlyList<ImportRow> Read(Stream stream)
    {
        try { return Parse(stream); }
        catch (Exception e) when (e is InvalidDataException or XmlException or FormatException
            or OverflowException or ArgumentException or InvalidOperationException or IOException
            or IndexOutOfRangeException or NullReferenceException)
        { throw Invalid(); }
    }
    private static IReadOnlyList<ImportRow> Parse(Stream stream)
    {
        if (!stream.CanSeek || stream.Length is <= 0 or > MaxBytes) throw Invalid();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        if (zip.Entries.Count > 100 || zip.Entries.Sum(e => e.Length) > 32 * 1024 * 1024
            || zip.Entries.Any(e => e.Length > 16 * 1024 * 1024)
            || zip.Entries.Select(e => e.FullName).Distinct().Count() != zip.Entries.Count) throw Invalid();
        XDocument ReadXml(string path)
        {
            using var input = (zip.GetEntry(path) ?? throw Invalid()).Open();
            using var reader = XmlReader.Create(input, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
            return XDocument.Load(reader);
        }
        var workbook = ReadXml("xl/workbook.xml");
        var date1904 = (string?)workbook.Root?.Element(S + "workbookPr")?.Attribute("date1904");
        if (date1904 is "1" or "true") throw Invalid();
        var rels = ReadXml("xl/_rels/workbook.xml.rels").Root!.Elements().ToArray();
        if (rels.Any(r => (string?)r.Attribute("TargetMode") == "External")) throw Invalid();
        var strings = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : ReadXml("xl/sharedStrings.xml")
            .Root!.Elements(S + "si").Select(si => string.Concat(si.Descendants(S + "t").Select(t => t.Value))).ToArray();
        var sheets = workbook.Root!.Element(S + "sheets")!.Elements(S + "sheet").ToArray();
        if (sheets.Length != 3) throw Invalid();
        var expected = new Dictionary<string, string> { ["Kiadások"] = "Expense", ["Bevétel"] = "Income", ["Átutalás"] = "Transfer" };
        var result = new List<ImportRow>();
        foreach (var sheet in sheets)
        {
            var name = (string)sheet.Attribute("name")!;
            if (!expected.Remove(name, out var kind)) throw Invalid();
            var id = (string?)sheet.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"));
            var target = (string?)rels.Single(r => (string?)r.Attribute("Id") == id).Attribute("Target") ?? throw Invalid();
            var path = target.StartsWith('/') ? target[1..] : "xl/" + target;
            if (!path.StartsWith("xl/worksheets/", StringComparison.Ordinal) || path.Contains("..")) throw Invalid();
            var document = ReadXml(path);
            if (document.Descendants(S + "f").Any()) throw Invalid();
            var rows = document.Root!.Element(S + "sheetData")!.Elements(S + "row").ToArray();
            if (rows.Length > MaxRows + 2) throw Invalid();
            string[] Cells(XElement row, int width)
            {
                var values = new string[width]; Array.Fill(values, "");
                var seen = new HashSet<int>();
                foreach (var cell in row.Elements(S + "c"))
                {
                    var reference = (string?)cell.Attribute("r") ?? throw Invalid();
                    var column = 0;
                    foreach (var c in reference.TakeWhile(char.IsAsciiLetterUpper)) column = checked(column * 26 + c - 'A' + 1);
                    if (column < 1 || column > width || !seen.Add(column)) throw Invalid();
                    var value = cell.Element(S + "v")?.Value ?? "";
                    value = (string?)cell.Attribute("t") switch {
                        "s" => strings[int.Parse(value, CultureInfo.InvariantCulture)],
                        "inlineStr" => string.Concat(cell.Descendants(S + "t").Select(t => t.Value)),
                        "e" or "b" => throw Invalid(), _ => value };
                    if (value.Length > 2000) throw Invalid();
                    values[column - 1] = value.Trim();
                }
                return values;
            }
            var headers = kind == "Transfer" ? TransferHeaders : TransactionHeaders;
            var header = rows.SingleOrDefault(r => (string?)r.Attribute("r") == "2") ?? throw Invalid();
            if (!Cells(header, headers.Length).SequenceEqual(headers)) throw Invalid();
            foreach (var row in rows.Where(r => int.Parse((string)r.Attribute("r")!, CultureInfo.InvariantCulture) > 2))
            {
                var c = Cells(row, headers.Length);
                if (c.All(string.IsNullOrWhiteSpace)) continue;
                if (result.Count >= MaxRows) throw Invalid();
                var serial = decimal.Parse(c[0], CultureInfo.InvariantCulture);
                if (serial < 61 || serial > 2958465 || serial != decimal.Truncate(serial)) throw Invalid();
                var date = DateOnly.FromDateTime(new DateTime(1899, 12, 30).AddDays((double)serial));
                var number = int.Parse((string)row.Attribute("r")!, CultureInfo.InvariantCulture);
                string Required(string value, int max) => value.Length is 0 || value.Length > max ? throw Invalid() : value;
                decimal Amount(string value) {
                    var amount = decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                    return Transfer.IsValidAmount(amount)
                        ? decimal.Parse(amount.ToString("G29", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) : throw Invalid();
                }
                string Currency(string value) => CurrencyCode.IsSupported(value) ? value : throw Invalid();
                if (kind == "Transfer")
                {
                    if (c[7].Length > 500 || string.IsNullOrEmpty(c[5]) != string.IsNullOrEmpty(c[6])) throw Invalid();
                    result.Add(new(name, number, kind, date, Required(c[1], 100), Required(c[2], 100), "",
                        Amount(c[3]), Currency(c[4]), c[5] == "" ? null : Amount(c[5]), c[6] == "" ? null : Currency(c[6]), "", c[7]));
                }
                else
                {
                    // Original FX data cannot be silently discarded in this first adapter.
                    if (c[7] != "" || c[8] != "" || c[9].Length > 160 || c[10].Length > 500) throw Invalid();
                    if (Amount(c[3]) != Amount(c[5]) || Currency(c[4]) != Currency(c[6])) throw Invalid();
                    result.Add(new(name, number, kind, date, Required(c[2], 100), null, Required(c[1], 100),
                        Amount(c[5]), Currency(c[6]), null, null, c[9], c[10]));
                }
            }
        }
        if (result.Count == 0) throw Invalid();
        return result;
    }
}
