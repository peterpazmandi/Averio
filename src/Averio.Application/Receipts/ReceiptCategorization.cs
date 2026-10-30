using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Domain.Transactions;

namespace Averio.Application.Receipts;

public sealed record ReceiptCategoryOption(Guid Id, string Path);
public sealed record RememberReceiptCategory(int LineIndex, int ItemIndex);
public interface IReceiptCategoryRuleStore
{
    Task<IReadOnlyDictionary<string, Guid>> GetAsync(Guid householdId, CancellationToken ct);
    Task UpsertAsync(Guid householdId, Guid memberId, string matchKey, Guid categoryId, DateTimeOffset now, CancellationToken ct);
}

public sealed class ReceiptCategorization(ICategoryRepository categories, IReceiptCategoryRuleStore rules)
{
    public async Task<IReadOnlyList<ReceiptCategoryOption>> CatalogAsync(Guid householdId, CancellationToken ct)
    {
        var available = await categories.GetAvailableAsync(householdId, TransactionKind.Expense, ct);
        var byId = available.Where(c => !c.IsArchived && c.IsAvailableTo(householdId)).ToDictionary(c => c.Id);
        return byId.Values.Where(c => c.SystemKey != "uncategorized").OrderBy(c => c.Id).Select(c =>
        {
            var path = new List<string> { c.NameReference };
            var seen = new HashSet<Guid> { c.Id };
            var parent = c.ParentId;
            while (parent is Guid id && byId.TryGetValue(id, out var p) && seen.Add(id) && path.Count < 10)
            { path.Insert(0, p.NameReference); parent = p.ParentId; }
            return new ReceiptCategoryOption(c.Id, string.Join(" > ", path));
        }).ToArray();
    }

    // Called after authorization. Load once per inbox page, not once per line.
    public async Task<ReceiptScanResult[]> ApplyAsync(Guid householdId, ReceiptScanResult[] scans, CancellationToken ct)
    {
        if (!scans.Any(s => s.Draft is not null)) return scans;
        var catalog = await CatalogAsync(householdId, ct);
        var allowed = catalog.Select(c => c.Id).ToHashSet();
        var remembered = await rules.GetAsync(householdId, ct);
        return scans.Select(scan => scan.Draft is not { } draft ? scan : scan with
        {
            Draft = draft with { Lines = draft.Lines.Select(line =>
            {
                Guid? category = line.Kind == "Item" && line.SuggestedCategoryId is Guid id && allowed.Contains(id) ? id : null;
                var source = category is null ? null : "Model";
                var key = MatchKey(draft.Merchant, line.Description);
                if (line.Kind == "Item" && key is not null && remembered.TryGetValue(key, out var saved) && allowed.Contains(saved))
                { category = saved; source = "HouseholdRule"; }
                return line with { SuggestedCategoryId = category, CategorySuggestionSource = source };
            }).ToArray() }
        }).ToArray();
    }

    public async Task RememberAsync(Guid householdId, Guid memberId, ReceiptDraft? draft,
        CreateTransactionCommand command, IReadOnlyList<RememberReceiptCategory> selections, DateTimeOffset now, CancellationToken ct)
    {
        if (selections.Count == 0) return;
        if (draft is null || selections.Count > 100 || selections.Select(s => s.LineIndex).Distinct().Count() != selections.Count
            || selections.Select(s => s.ItemIndex).Distinct().Count() != selections.Count) throw Invalid();
        var chosen = new Dictionary<string, Guid>();
        foreach (var selection in selections)
        {
            if (selection.LineIndex < 0 || selection.LineIndex >= draft.Lines.Length
                || selection.ItemIndex < 0 || selection.ItemIndex >= command.Items.Count) throw Invalid();
            var line = draft.Lines[selection.LineIndex];
            var key = MatchKey(draft.Merchant, line.Description);
            if (line.Kind != "Item" || key is null) throw Invalid();
            var category = command.Items[selection.ItemIndex].CategoryId;
            if (chosen.TryGetValue(key, out var previous) && previous != category) throw Invalid();
            chosen[key] = category;
        }
        // Same category locks as ledger validation; no archived, foreign or uncategorized targets.
        var valid = await categories.GetAvailableByIdsAsync(householdId, chosen.Values.Distinct().ToArray(), ct);
        if (chosen.Values.Any(id => !valid.Any(c => c.Id == id && c.IsAvailableTo(householdId)
            && !c.IsArchived && c.Supports(TransactionKind.Expense) && c.SystemKey != "uncategorized"))) throw Invalid();
        foreach (var (key, category) in chosen)
            await rules.UpsertAsync(householdId, memberId, key, category, now, ct);
    }

    // Exact merchant + printed item match after conservative Unicode/whitespace normalization.
    // Hashes avoid retaining receipt text in the shared rule table. No fuzzy matching or personal history.
    public static string? MatchKey(string? merchant, string description)
    {
        static string Normalize(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ").ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(merchant) || string.IsNullOrWhiteSpace(description)) return null;
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] { Normalize(merchant), Normalize(description) })));
    }
    private static BusinessRuleException Invalid() => new("receipt_category_rule_invalid", "Error_ReceiptCategoryRuleInvalid");
}
