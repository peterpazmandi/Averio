using Averio.Application.Common.Exceptions;
using Averio.Application.Imports;
using Averio.Application.PersonalMoney;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.Refunds;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Imports;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

internal static class RefundCreditReversalVerification
{
    public static async Task RunAsync(IServiceProvider services, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        var ct = CancellationToken.None;
        user.UserId = f.OwnerUserId;
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        Guid accountId, labelId, secondIncomeCategory;
        await using (var s = services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = Account.Create(f.HouseholdId, [], "Refund reversal", AccountType.Checking, AccountScope.Household,
                AccountVisibility.Household, true, CurrencyCode.Eur, 0, day.AddDays(-10), clock.GetUtcNow());
            db.Add(account);
            var label = Averio.Domain.Labels.Label.Create(f.HouseholdId, null, Averio.Domain.Labels.LabelScope.Household, "Original credit label", clock.GetUtcNow());
            db.Add(label);
            await db.SaveChangesAsync(); accountId = account.Id; labelId = label.Id;
            secondIncomeCategory = await db.Categories.Where(x => x.IsBase && x.Kind == Averio.Domain.Categories.CategoryKind.Income && x.Id != TestIds.Salary).Select(x => x.Id).FirstAsync();
        }

        // Upgrade a populated old schema. Both active and already-cancelled history must survive unchanged.
        var historicalPurchase = await Post(TransactionKind.Expense, [new("Legacy", TestIds.Groceries, 100)]);
        var historical = await Link(historicalPurchase, null);
        var cancelledPurchase = await Post(TransactionKind.Expense, [new("Legacy cancelled", TestIds.Groceries, 100)]);
        var cancelled = await Link(cancelledPurchase, null);
        await Cancel(cancelledPurchase, cancelled, (await Read(cancelledPurchase)).UpdatedAt);
        var beforeUpgrade = await Balance();
        await using (var s = services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AverioDbContext>();
            // Reconstruct only the pre-provenance schema in this disposable test database.
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE item_refunds DROP CONSTRAINT ck_item_refunds_origin;
                ALTER TABLE item_refunds DROP COLUMN credit_origin, DROP COLUMN original_income_json;
                DROP INDEX "IX_item_refunds_credit_transaction_id";
                CREATE UNIQUE INDEX "IX_item_refunds_credit_transaction_id" ON item_refunds(credit_transaction_id);
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%_PreserveRefundCreditOrigin';
                """);
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync();
            Check((await db.Set<ItemRefund>().SingleAsync(x => x.Id == historical.Id)).CreditOrigin == RefundCreditOrigin.Unknown, "Legacy origin was guessed");
            Check((await db.Set<ItemRefund>().SingleAsync(x => x.Id == cancelled.Id)).CancelledAt != null, "Migration resurrected cancelled history");
        }
        await Throws<BusinessRuleException>(async () => await Cancel(historicalPurchase, historical, await ReadVersion(historicalPurchase)), "Unknown origin reversal");
        await Cancel(cancelledPurchase, cancelled, (await Read(cancelledPurchase)).UpdatedAt);
        Check(await Balance() == beforeUpgrade && (await Read(historical.CreditTransactionId)).Status == TransactionStatus.Posted, "Legacy safeguard changed cash");

        var available = await Allowance();
        var purchaseId = await Post(TransactionKind.Expense, [new("Purchased item", TestIds.Groceries, 100, PersonalMoneyAmount: 40)]);
        var creditId = await Post(TransactionKind.Income, [new("Original one", TestIds.Salary, 35.0001m), new("Original two", secondIncomeCategory, 64.9999m)]);
        Guid batchId;
        await using (var s = services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AverioDbContext>();
            var batch = ImportBatch.Create(f.HouseholdId, f.OwnerMemberId, new string('A', 64), "{}", clock.GetUtcNow(), "bank.csv");
            batch.Complete(clock.GetUtcNow(), "[{\"id\":\"" + creditId + "\"}]", "{}");
            db.Add(batch); (await db.Transactions.SingleAsync(x => x.Id == creditId)).SetImportOrigin(batch.Id);
            await db.SaveChangesAsync(); batchId = batch.Id;
        }
        var original = await Read(creditId);
        var cash = await Balance();
        var stats = await Aggregates();
        var historicalCash = await Balance(day.AddDays(-1));
        var refund = await Link(purchaseId, creditId);
        Check(refund.CreditOrigin == RefundCreditOrigin.ExistingIncome, "Missing durable origin");
        Check(await Balance() == cash && await Allowance() == available, "Link changed cash or proportional allowance");
        var purchaseVersion = (await Read(purchaseId)).UpdatedAt;
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Cancel(purchaseId, refund, purchaseVersion)));
        var restored = await Read(creditId);
        Check(restored.Kind == TransactionKind.Income && restored.Status == TransactionStatus.Posted && restored.VoidedAt == null, "Existing credit was voided");
        Check(restored.Id == original.Id && restored.AccountId == original.AccountId && restored.OccurredOn == original.OccurredOn
            && restored.Amount == original.Amount && restored.Currency == original.Currency && restored.CreatedAt == original.CreatedAt
            && restored.CreatedByHouseholdMemberId == original.CreatedByHouseholdMemberId && restored.Visibility == original.Visibility
            && restored.Description == original.Description && restored.Note == original.Note && restored.Merchant == original.Merchant
            && restored.ImportBatchId == batchId && restored.UpdatedAt > original.UpdatedAt
            && restored.Labels.Select(x => x.LabelId).SequenceEqual(original.Labels.Select(x => x.LabelId)), "Original credit metadata lost");
        Check(await Balance(day.AddDays(-1)) == historicalCash, "Reversal changed cash before the credit date");
        Check(Items(restored) == Items(original), "Original item identity/order/category snapshots lost");
        Check(await Balance() == cash && await Aggregates() == stats && await Allowance() == available - 40, "Reversal changed cash/aggregates/allowance incorrectly");
        await using (var s = services.CreateAsyncScope())
        {
            var imports = s.ServiceProvider.GetRequiredService<IImportRepository>();
            var overlap = await imports.FindOverlapAsync(f.HouseholdId, f.OwnerMemberId, [accountId], day.AddDays(-10), day, ct);
            Check(overlap.Single(x => x.Id == creditId).Kind == "Income", "Import overlap retained refund classification");
            Check((await imports.LinkedRecordsAsync(f.HouseholdId, [creditId], ct)).Contains(creditId), "Import undo can delete retained audit references");
            var db = s.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check((await db.Set<ImportBatch>().SingleAsync(x => x.Id == batchId)).RecordLinks!.Contains(creditId.ToString()), "Import links lost");
            await Throws<NotFoundException>(() => s.ServiceProvider.GetRequiredService<ItemRefundHandler>().OriginAsync(f.HouseholdId, creditId, ct), "Independent income exposed old origin");
        }

        // Competing re-links use one credit revision: only one can win. Reversed wallet history is not an active dependency.
        var fresh = await Read(purchaseId);
        var request = new RecordItemRefund(fresh.Items.Single().Id, 100, original.OccurredOn, Guid.NewGuid(), fresh.UpdatedAt, true, creditId, restored.UpdatedAt);
        async Task<Guid?> Race(RecordItemRefund input) { try { return await Record(purchaseId, input); } catch (ConflictException) { return null; } }
        var competingRequest = request with { OperationId = Guid.NewGuid() };
        var winners = await Task.WhenAll(Race(request), Race(competingRequest));
        Check(winners.Count(x => x != null) == 1, "Competing links both succeeded");
        var active = (await List(purchaseId)).Single(x => x.CancelledAt == null);
        await Cancel(purchaseId, refund, purchaseVersion); // Lost response retry from the previous link must not cancel the new one.
        Check((await Read(creditId)).Kind == TransactionKind.Refund && await Allowance() == available, "Old reversal retry affected new link");
        await Cancel(purchaseId, active, (await Read(purchaseId)).UpdatedAt);
        Check(await Record(purchaseId, winners[0] is not null ? request : competingRequest) == active.Id
            && (await Read(creditId)).Kind == TransactionKind.Income, "Late original record retry recreated a reversed refund");
        Check(await Balance() == cash && await Aggregates() == stats && await Allowance() == available - 40, "Repeated link/reversal drift");

        // A newly created refund still disappears from cash on reversal, exactly once.
        var created = await Link(purchaseId, null);
        var createdVersion = (await Read(purchaseId)).UpdatedAt;
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Cancel(purchaseId, created, createdVersion)));
        Check(created.CreditOrigin == RefundCreditOrigin.Created && (await Read(created.CreditTransactionId)).Status == TransactionStatus.Voided,
            "Created refund did not retain void behavior");
        Check(await Balance() == cash && await Aggregates() == stats && await Allowance() == available - 40, "Created reversal double-applied");

        await using (var s = services.CreateAsyncScope())
        {
            var income = await Read(creditId);
            await s.ServiceProvider.GetRequiredService<UpdateTransactionHandler>().HandleAsync(new(f.HouseholdId, income.Id,
                income.AccountId, income.Kind, income.Visibility, income.Merchant, "Independent income edit", income.OccurredOn,
                income.Items.OrderBy(x => x.Position).Select(x => new TransactionItemInput(x.Description, x.CategoryId, x.Amount, x.Id)).ToArray(),
                income.Labels.Select(x => x.LabelId).ToArray(), income.UpdatedAt), ct);
            Check((await Read(creditId)).Note == "Independent income edit", "Cancelled refund history still locks independent income");
        }

        // Private details and restoration are unavailable to the other household member.
        var privatePurchase = await Post(TransactionKind.Expense, [new("Private purchase", TestIds.Groceries, 100)], true);
        var privateCredit = await Post(TransactionKind.Income, [new("Private income", TestIds.Salary, 100)], true);
        var privateRefund = await Link(privatePurchase, privateCredit);
        var privateVersion = (await Read(privatePurchase)).UpdatedAt;
        user.UserId = f.MemberUserId;
        await Throws<NotFoundException>(() => Cancel(privatePurchase, privateRefund, privateVersion), "Private reversal");
        await Throws<NotFoundException>(() => List(privatePurchase), "Private history");
        user.UserId = f.OwnerUserId;
        await Cancel(privatePurchase, privateRefund, privateVersion);
        Check((await Read(privateCredit)).Visibility == TransactionVisibility.Private, "Restoration changed privacy");
        user.UserId = f.MemberUserId;
        await using (var s = services.CreateAsyncScope())
            Check(await s.ServiceProvider.GetRequiredService<Averio.Application.Common.Abstractions.ITransactionRepository>()
                .GetVisibleByIdAsync(f.HouseholdId, f.MemberId, privateCredit, ct) == null, "Restored private income leaked");
        user.UserId = f.OwnerUserId;
        await using (var s = services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AverioDbContext>();
            var migrations = db.Database.GetMigrations().ToArray();
            var previousMigration = migrations[Array.FindIndex(migrations, x => x.EndsWith("_PreserveRefundCreditOrigin")) - 1];
            try
            {
                await db.GetService<IMigrator>().MigrateAsync(previousMigration);
                throw new Exception("Downgrade discarded durable provenance");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "P0001" && ex.MessageText.Contains("Cannot discard refund provenance")) { }
            // EF can finish newer, empty-schema downgrades before reaching this guard.
            // The provenance migration itself and its data must remain intact.
            Check((await db.Database.GetAppliedMigrationsAsync()).Any(x => x.EndsWith("_PreserveRefundCreditOrigin")), "Rejected downgrade removed provenance migration");
            await db.Database.MigrateAsync();
            Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Could not restore later migrations after guarded downgrade");
            Check((await db.Set<ItemRefund>().SingleAsync(x => x.Id == refund.Id)).OriginalIncomeJson != null, "Rejected downgrade lost snapshot");
        }
        Console.WriteLine("PASS refund provenance: populated migration, legacy safeguard, imported split credit restoration, aggregates/cash, Personal Money, competing relinks, stale retries, created-credit voiding and privacy");

        async Task<Guid> Post(TransactionKind kind, IReadOnlyList<TransactionItemInput> items, bool personal = false)
        {
            await using var s = services.CreateAsyncScope();
            return await s.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(new(f.HouseholdId,
                personal ? f.PrivateAccountId : accountId, kind, personal ? TransactionVisibility.Private : TransactionVisibility.Household,
                "Original merchant", "Original note", kind == TransactionKind.Expense ? day.AddDays(-2) : day, items, kind == TransactionKind.Income ? [labelId] : []), ct);
        }
        async Task<Transaction> Read(Guid id) { await using var s = services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<AverioDbContext>().Transactions.AsNoTracking().Include(x => x.Items).Include(x => x.Labels).SingleAsync(x => x.Id == id); }
        async Task<DateTimeOffset> ReadVersion(Guid id) => (await Read(id)).UpdatedAt;
        async Task<Guid> Record(Guid id, RecordItemRefund input) { await using var s = services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ItemRefundHandler>().RecordAsync(f.HouseholdId, id, input, ct); }
        async Task<IReadOnlyList<ItemRefund>> List(Guid id) { await using var s = services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ItemRefundHandler>().ListAsync(f.HouseholdId, id, ct); }
        async Task<ItemRefund> Link(Guid id, Guid? credit)
        {
            var p = await Read(id); var c = credit is Guid cid ? await Read(cid) : null;
            var rid = await Record(id, new(p.Items.Single().Id, 100, day, Guid.NewGuid(), p.UpdatedAt, true, credit, c?.UpdatedAt));
            return (await List(id)).Single(x => x.Id == rid);
        }
        async Task Cancel(Guid id, ItemRefund r, DateTimeOffset version) { await using var s = services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<ItemRefundHandler>().CancelAsync(f.HouseholdId, id, r.Id, r.Version, version, ct); }
        async Task<decimal> Balance(DateOnly? date = null) { await using var s = services.CreateAsyncScope(); return (await s.ServiceProvider.GetRequiredService<Averio.Application.Common.Abstractions.IAccountBalanceReader>().GetCurrentBalancesAsync(f.HouseholdId, f.OwnerMemberId, [accountId], date ?? day, ct))[accountId]; }
        async Task<decimal> Allowance() { await using var s = services.CreateAsyncScope(); return (await s.ServiceProvider.GetRequiredService<PersonalMoneyHandler>().GetMineAsync(f.HouseholdId, ct))!.Available; }
        async Task<string> Aggregates()
        {
            await using var s = services.CreateAsyncScope();
            var rows = await s.ServiceProvider.GetRequiredService<AverioDbContext>().TransactionAggregates.AsNoTracking().Where(x => x.AccountId == accountId).ToArrayAsync();
            return string.Join("\n", rows.Where(x => x.IncomeAmount != 0 || x.ExpenseAmount != 0 || x.EntryCount != 0).Select(x => System.Text.Json.JsonSerializer.Serialize(new { x.HouseholdId, x.AccountId, x.DimensionType, x.DimensionId, x.Currency, x.Period, x.PeriodStart, x.IncomeAmount, x.ExpenseAmount, x.EntryCount })).Order());
        }
    }
    private static string Items(Transaction t) => System.Text.Json.JsonSerializer.Serialize(t.Items.OrderBy(x => x.Position));
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action, string scenario) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception("Expected rejection: " + scenario); }
}
