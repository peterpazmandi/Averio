using System.Text.Json;
using Averio.Application.Common.Exceptions;
using Averio.Application.Receipts;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Domain.Accounts;
using Averio.Domain.Categories;
using Averio.Domain.Common;
using Averio.Domain.Receipts;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class ReceiptPostingVerification
{
    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        user.UserId = f.OwnerUserId;
        var now = clock.GetUtcNow();
        Guid accountId, category;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = Account.Create(f.HouseholdId, [], "Receipt posting test", AccountType.Checking,
                AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Eur, 0,
                DateOnly.FromDateTime(now.UtcDateTime), now);
            db.Add(account); await db.SaveChangesAsync(); accountId = account.Id;
            category = await db.Categories.Where(x => x.IsBase && !x.IsArchived && x.Kind == CategoryKind.Expense).Select(x => x.Id).FirstAsync();
        }
        var command = new CreateTransactionCommand(f.HouseholdId, accountId, TransactionKind.Expense,
            TransactionVisibility.Household, "Reviewed shop", null, DateOnly.FromDateTime(now.UtcDateTime),
            [new TransactionItemInput("Reviewed item", category, 9m)], []);
        async Task<ReceiptScan> Draft(string status = "Ready", bool expired = false)
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var row = ReceiptScan.Create(f.HouseholdId, f.OwnerMemberId, Guid.NewGuid(), new string('a', 64), 1,
                expired ? now.AddDays(-8) : now);
            db.Add(row); await db.SaveChangesAsync();
            string? draft = status == "Ready" ? JsonSerializer.Serialize(new ReceiptDraft("Shop", null, "EUR", "9", [], [])) : null;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE receipt_scans SET status = {status}, draft_json = {draft} WHERE id = {row.Id}");
            return row;
        }
        async Task<Guid> Post(ReceiptScan scan, CreateTransactionCommand? request = null, string currency = "EUR", decimal total = 9m, Guid? version = null)
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<PostReceiptHandler>().HandleAsync(f.HouseholdId,
                scan.Id, version ?? scan.Version, currency, total, request ?? command, default);
        }
        async Task<string> Fingerprint()
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            return JsonSerializer.Serialize(new {
                Transactions = await db.Transactions.CountAsync(),
                Aggregates = await db.TransactionAggregates.AsNoTracking().OrderBy(x => x.AccountId).ThenBy(x => x.DimensionType).ThenBy(x => x.DimensionId).ThenBy(x => x.Period).ThenBy(x => x.PeriodStart).ToListAsync(),
                Entries = await db.PersonalMoneyEntries.CountAsync()
            });
        }
        var ready = await Draft();
        var before = await Fingerprint();
        user.UserId = f.MemberUserId;
        await Reject<NotFoundException>(() => Post(ready));
        user.UserId = f.OwnerUserId;
        await Reject<ConflictException>(() => Post(ready, version: Guid.NewGuid()));
        await Reject<BusinessRuleException>(() => Post(ready, total: 10m));
        await Reject<BusinessRuleException>(() => Post(ready, currency: "HUF"));
        await Reject<BusinessRuleException>(() => Post(ready, command with { Kind = TransactionKind.Income }));
        foreach (var status in new[] { "Uploading", "Queued", "Failed", "Discarded" })
        {
            var invalid = await Draft(status);
            await Reject<ConflictException>(() => Post(invalid));
        }
        var expired = await Draft(expired: true);
        await Reject<ConflictException>(() => Post(expired));
        Check(before == await Fingerprint(), "invalid receipt posting changed financial state");
        // This rule fails after transaction and aggregate writes. The outer
        // receipt transaction must roll all of those writes back.
        var badAllocation = command with { ChargePersonalMoney = true,
            Items = [new TransactionItemInput("Item", category, 9m, PersonalMoneyAmount: 1m)] };
        await Reject<BusinessRuleException>(() => Post(ready, badAllocation));
        Check(before == await Fingerprint(), "posting failure did not roll back ledger and aggregates");
        var results = await Task.WhenAll(Post(ready), Post(ready));
        Check(results[0] == results[1], "concurrent confirmation created multiple transactions");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var row = await db.Set<ReceiptScan>().AsNoTracking().SingleAsync(x => x.Id == ready.Id);
            Check(row.Status == ReceiptScanStatus.Posted && row.PostedTransactionId == results[0] && row.DraftJson == null, "posted link not atomic");
            Check(await db.Transactions.CountAsync(x => x.AccountId == accountId) == 1, "posting wrote multiple ledger rows");
            Check(await db.Transactions.Where(x => x.Id == results[0]).Select(x => x.Amount).SingleAsync() == 9m, "posting lost reviewed total");
            await Reject<ConflictException>(() => scope.ServiceProvider.GetRequiredService<ReceiptHandler>()
                .DiscardAsync(f.HouseholdId, ready.Id, row.Version, default));
        }
        var allocated = await Draft();
        var withAllocation = command with { Items = [new TransactionItemInput("Personal item", category, 9m, PersonalMoneyAmount: 2m)] };
        var allocatedResults = await Task.WhenAll(Post(allocated, withAllocation), Post(allocated, withAllocation));
        Check(allocatedResults[0] == allocatedResults[1], "allocation confirmation was not idempotent");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var entries = await db.PersonalMoneyEntries.AsNoTracking().Where(x => x.TransactionId == allocatedResults[0]).ToListAsync();
            Check(entries.Count == 1 && entries[0].Amount == -2m && entries[0].TransactionItemId != null,
                "receipt retry charged Personal Money more than once or lost item attribution");
        }
        var after = await Fingerprint();
        clock.Advance(TimeSpan.FromDays(8));
        try
        {
            Check(await Post(ready) == results[0], "confirmation replay after expiry lost original transaction");
            using var scope = provider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IReceiptRepository>();
            Check(await repo.ExpireAsync(ready.Id, clock.GetUtcNow(), default), "posted image cleanup was not reserved");
            await repo.MarkDocumentDeletedAsync(ready.Id, clock.GetUtcNow(), default);
            var row = await repo.GetAsync(f.HouseholdId, f.OwnerMemberId, ready.Id, default);
            Check(row!.Status == ReceiptScanStatus.Posted && row.PostedTransactionId == results[0] && row.DocumentDeletedAt != null, "cleanup destroyed posting idempotency");
            Check(await Post(ready) == results[0] && after == await Fingerprint(), "replay or cleanup changed financial state");
        }
        finally { clock.Advance(TimeSpan.FromDays(-8)); }
        user.UserId = f.MemberUserId;
        await Reject<NotFoundException>(() => Post(ready));
        user.UserId = f.OwnerUserId;
        Console.WriteLine("PASS receipt posting privacy, reconciliation, rollback, concurrency, replay and cleanup");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Reject<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
}
