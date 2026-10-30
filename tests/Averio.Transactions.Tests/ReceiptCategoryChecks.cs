using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Receipts;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Domain.Categories;
using Averio.Domain.Transactions;

internal static class ReceiptCategoryChecks
{
    public static async Task RunAsync()
    {
        var household = Guid.NewGuid(); var member = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var food = Category.Create(household, "Food", CategoryKind.Expense, "food", "#10B981", null, now);
        var dairy = Category.Create(household, "Dairy", CategoryKind.Expense, "food", "#10B981", food, now);
        var foreign = Category.Create(Guid.NewGuid(), "Foreign", CategoryKind.Expense, "food", "#10B981", null, now);
        var repo = new Categories([food, dairy, foreign]); var store = new Rules();
        var service = new ReceiptCategorization(repo, store);
        var catalog = await service.CatalogAsync(household, default);
        Check(catalog.Count == 2 && catalog.Single(c => c.Id == dairy.Id).Path == "Food > Dairy", "authorized category paths");
        var draft = new ReceiptDraft("Tesco", null, "HUF", "3", [
            new("TS KEFIR 175G", "1", "Item", SuggestedCategoryId: dairy.Id),
            new("Unknown", "1", "Item", SuggestedCategoryId: foreign.Id),
            new("Payment", "3", "Payment", SuggestedCategoryId: dairy.Id)], []);
        var scan = new ReceiptScanResult(Guid.NewGuid(), "Ready", Guid.NewGuid(), 1, now, now.AddDays(1), null, draft);
        async Task<ReceiptDraft> Apply(Guid h) => (await service.ApplyAsync(h, [scan], default))[0].Draft!;
        var suggested = await Apply(household);
        Check(suggested.Lines[0].SuggestedCategoryId == dairy.Id && suggested.Lines[0].CategorySuggestionSource == "Model", "valid model suggestion");
        Check(suggested.Lines.Skip(1).All(l => l.SuggestedCategoryId is null), "foreign and payment suggestions discarded");
        var key = ReceiptCategorization.MatchKey(" Tesco ", " ts   kefir 175g ")!;
        Check(key == ReceiptCategorization.MatchKey("Tesco", "TS KEFIR 175G"), "normalized exact match");
        Check(key != ReceiptCategorization.MatchKey("Other shop", "TS KEFIR 175G")
            && key != ReceiptCategorization.MatchKey("Tesco", "TS KEFIR 500G"), "merchant and size stay distinct");
        var command = new CreateTransactionCommand(household, Guid.NewGuid(), TransactionKind.Expense,
            TransactionVisibility.Household, "Changed merchant", null, new DateOnly(2026, 10, 30),
            [new TransactionItemInput("Edited friendly name", food.Id, 3m)], []);
        await service.RememberAsync(household, member, draft, command, [], now, default);
        Check(store.Values.Count == 0, "no implicit learning");
        await service.RememberAsync(household, member, draft, command, [new(0, 0)], now, default);
        suggested = await Apply(household);
        Check(suggested.Lines[0].SuggestedCategoryId == food.Id && suggested.Lines[0].CategorySuggestionSource == "HouseholdRule", "household preference wins using original evidence");
        Check((await Apply(Guid.NewGuid())).Lines[0].SuggestedCategoryId is null, "household isolation");
        await Reject(() => service.RememberAsync(household, member, draft, command, [new(2, 0)], now, default));
        await Reject(() => service.RememberAsync(household, member, draft, command, [new(0, 4)], now, default));
        await Reject(() => service.RememberAsync(household, member, draft, command, [new(0, 0), new(0, 0)], now, default));
        await Reject(() => service.RememberAsync(household, member, draft with { Merchant = null }, command, [new(0, 0)], now, default));
        await Reject(() => service.RememberAsync(household, member, draft, command with { Items = [new TransactionItemInput("X", foreign.Id, 3m)] }, [new(0, 0)], now, default));
        food.SetArchived(true, now.AddSeconds(1));
        Check((await Apply(household)).Lines[0].SuggestedCategoryId == dairy.Id, "archived rule target ignored");
        await Reject(() => service.RememberAsync(household, member, draft, command, [new(0, 0)], now, default));
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static async Task Reject(Func<Task> action)
    { try { await action(); } catch (BusinessRuleException) { return; } throw new Exception("Invalid rule accepted"); }
    private sealed class Categories(Category[] values) : ICategoryRepository
    {
        public Task<IReadOnlyList<Category>> GetAvailableAsync(Guid h, TransactionKind? kind, CancellationToken ct, bool includeArchived = false)
            => Task.FromResult<IReadOnlyList<Category>>(values.Where(c => c.Supports(kind ?? TransactionKind.Expense)).ToArray());
        public Task<IReadOnlyList<Category>> GetAvailableByIdsAsync(Guid h, IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Category>>(values.Where(c => ids.Contains(c.Id)).ToArray());
    }
    private sealed class Rules : IReceiptCategoryRuleStore
    {
        public readonly Dictionary<(Guid Household, string Key), Guid> Values = [];
        public Task<IReadOnlyDictionary<string, Guid>> GetAsync(Guid h, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, Guid>>(Values.Where(p => p.Key.Household == h).ToDictionary(p => p.Key.Key, p => p.Value));
        public Task UpsertAsync(Guid h, Guid m, string key, Guid category, DateTimeOffset now, CancellationToken ct)
        { Values[(h, key)] = category; return Task.CompletedTask; }
    }
}
