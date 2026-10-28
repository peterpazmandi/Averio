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
            Check(row.Status == ReceiptScanStatus.Posted && row.PostedTransactionId == results[0] && row.DraftJson == null
                && row.ConfirmationFingerprint is { Length: 64 }, "posted link and confirmation intent not atomic");
            Check(await db.Transactions.CountAsync(x => x.AccountId == accountId) == 1, "posting wrote multiple ledger rows");
            Check(await db.Transactions.Where(x => x.Id == results[0]).Select(x => x.Amount).SingleAsync() == 9m, "posting lost reviewed total");
            await Reject<ConflictException>(() => scope.ServiceProvider.GetRequiredService<ReceiptHandler>()
                .DiscardAsync(f.HouseholdId, ready.Id, row.Version, default));
        }
        var postedState = await Fingerprint();
        await Reject<ConflictException>(() => Post(ready, command with { AccountId = Guid.NewGuid() }));
        await Reject<ConflictException>(() => Post(ready, command with {
            Items = [new TransactionItemInput("Reviewed item", category, 90m)] }, total: 90m));
        await Reject<ConflictException>(() => Post(ready, command with {
            Items = [new TransactionItemInput("One", category, 4m), new TransactionItemInput("Two", category, 5m)] }));
        await Reject<ConflictException>(() => Post(ready, command with {
            Items = [new TransactionItemInput("Reviewed item", category, 9m, PersonalMoneyAmount: 1m)] }));
        await Reject<ConflictException>(() => Post(ready, command with { LabelIds = [Guid.NewGuid()] }));
        await Reject<ConflictException>(() => Post(ready, command with { Visibility = TransactionVisibility.Private }));
        await Reject<ConflictException>(() => Post(ready, command with { OccurredOn = command.OccurredOn.AddDays(1) }));
        await Reject<ConflictException>(() => Post(ready, currency: "HUF"));
        await Reject<ConflictException>(() => Post(ready, version: Guid.NewGuid()));
        Check(await Post(ready, command with { Items = [new TransactionItemInput("Reviewed item", category, 9.0000m)] }, total: 9.000m) == results[0],
            "equivalent decimal formatting must preserve confirmation identity");
        Check(postedState == await Fingerprint(), "changed confirmation altered ledger, aggregates or allowance entries");
        // Editing the posted ledger later must neither change original replay
        // identity nor make the edited metadata look like the original request.
        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE transactions SET merchant = {"Edited later"} WHERE id = {results[0]}");
        Check(await Post(ready) == results[0], "ledger metadata edit broke original confirmation replay");
        await Reject<ConflictException>(() => Post(ready, command with { Merchant = "Edited later" }));

        var competing = await Draft();
        async Task<Guid?> Attempt(CreateTransactionCommand request, decimal total)
        {
            try { return await Post(competing, request, total: total); }
            catch (ConflictException) { return null; }
        }
        var competingResults = await Task.WhenAll(Attempt(command, 9m), Attempt(command with {
            Items = [new TransactionItemInput("Different confirmation", category, 90m)] }, 90m));
        Check(competingResults.Count(x => x is not null) == 1, "different concurrent confirmations must have one success and one conflict");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check(await db.Transactions.CountAsync(x => x.AccountId == accountId) == 2, "competing requests posted more than one additional expense");
            var row = await db.Set<ReceiptScan>().AsNoTracking().SingleAsync(x => x.Id == competing.Id);
            Check(row.PostedTransactionId == competingResults.Single(x => x is not null), "competing confirmation link differs from its winner");
        }
        var splitScan = await Draft();
        var splitRequest = command with { Items = [new TransactionItemInput("One", category, 4m), new TransactionItemInput("Two", category, 5m)] };
        var splitId = await Post(splitScan, splitRequest);
        await Reject<ConflictException>(() => Post(splitScan, splitRequest with {
            Items = [new TransactionItemInput("One", category, 5m), new TransactionItemInput("Two", category, 4m)] }));
        await Reject<ConflictException>(() => Post(splitScan, splitRequest with { Items = splitRequest.Items.Reverse().ToArray() }));
        Check(await Post(splitScan, splitRequest) == splitId, "unchanged split confirmation did not replay");

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
        await Reject<ConflictException>(() => Post(allocated, withAllocation with {
            Items = [new TransactionItemInput("Personal item", category, 9m, PersonalMoneyAmount: 3m)] }));
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
            Check(row!.Status == ReceiptScanStatus.Posted && row.PostedTransactionId == results[0] && row.DocumentDeletedAt != null
                && row.ConfirmationFingerprint is { Length: 64 }, "cleanup destroyed posting idempotency");
            Check(await Post(ready) == results[0] && after == await Fingerprint(), "replay or cleanup changed financial state");
            await Reject<ConflictException>(() => Post(ready, command with { Note = "Changed after expiry" }));
        }
        finally { clock.Advance(TimeSpan.FromDays(-8)); }
        // Already-posted rows from the preceding schema have no trustworthy
        // original intent. Never reconstruct a fingerprint from mutable history.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            await Reject<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE receipt_scans SET confirmation_fingerprint = {"invalid"} WHERE id = {allocated.Id}"));
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE receipt_scans SET confirmation_fingerprint = NULL WHERE id = {allocated.Id}");
        }
        await Reject<ConflictException>(() => Post(allocated, withAllocation));
        Check(after == await Fingerprint(), "legacy or invalid fingerprint retry changed financial state");
        user.UserId = f.MemberUserId;
        await Reject<NotFoundException>(() => Post(ready));
        user.UserId = f.OwnerUserId;
        Console.WriteLine("PASS receipt posting privacy, reconciliation, rollback, concurrency, replay and cleanup");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Reject<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
}
