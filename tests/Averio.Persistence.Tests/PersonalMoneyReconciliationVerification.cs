using Averio.Application.Common.Exceptions;
using Averio.Application.PersonalMoney;
using Averio.Application.Statistics.GetBoundaryCashFlow;
using Averio.Application.Transfers;
using Averio.Domain.Categories;
using Averio.Domain.Common;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class PersonalMoneyReconciliationVerification
{
    private static readonly DateOnly On = new(2026, 10, 17);
    private static readonly CancellationToken Ct = CancellationToken.None;

    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user,
        Fixture fixture, MutableTimeProvider clock)
    {
        user.UserId = fixture.OwnerUserId;
        var baseline = await ReadAsync(provider, fixture);
        Guid sharedExpense, sharedRefund, privateExpense, privateRefund;
        string aggregateBefore;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            aggregateBefore = await AggregateFingerprintAsync(db);
            var expenseCategory = await db.Categories.AsNoTracking()
                .Where(x => x.IsBase && !x.IsArchived && x.Kind == CategoryKind.Expense).FirstAsync(Ct);
            var incomeCategory = await db.Categories.AsNoTracking()
                .Where(x => x.IsBase && !x.IsArchived && x.Kind == CategoryKind.Income).FirstAsync(Ct);
            Transaction Build(Guid accountId, TransactionKind kind,
                TransactionVisibility visibility, Guid categoryId, decimal amount)
                => Transaction.Create(fixture.HouseholdId, accountId, fixture.OwnerMemberId,
                    kind, visibility, CurrencyCode.Eur, "Reconciliation fixture", null, On,
                    [new TransactionItemDraft("Reconciliation", categoryId, "test", "cash", amount)],
                    [], clock.GetUtcNow());
            var s = Build(fixture.HouseholdAccountId, TransactionKind.Expense,
                TransactionVisibility.Household, expenseCategory.Id, 80m);
            var sr = Build(fixture.HouseholdAccountId, TransactionKind.Income,
                TransactionVisibility.Household, incomeCategory.Id, 20m);
            var p = Build(fixture.PrivateAccountId, TransactionKind.Expense,
                TransactionVisibility.Private, expenseCategory.Id, 50m);
            var pr = Build(fixture.PrivateAccountId, TransactionKind.Income,
                TransactionVisibility.Private, incomeCategory.Id, 10m);
            db.Transactions.AddRange(s, sr, p, pr);
            await db.SaveChangesAsync(Ct);
            sharedExpense = s.Id; sharedRefund = sr.Id;
            privateExpense = p.Id; privateRefund = pr.Id;
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var pm = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            var before = await pm.GetMineAsync(fixture.HouseholdId, Ct)
                ?? throw new Exception("Expected wallet.");
            await pm.ChargePrivateAsync(fixture.HouseholdId, sharedExpense, 80m, Guid.NewGuid(), Ct);
            var afterShared = await ReadAsync(provider, fixture);
            Equal(baseline.Available - 80m, afterShared.Available, "shared expense entitlement");
            Equal(baseline.AttributedPrivateFunding, afterShared.AttributedPrivateFunding,
                "shared expense must not alter private funding");
            await pm.CorrectChargeAsync(fixture.HouseholdId, sharedExpense,
                60m, 80m, 1, Guid.NewGuid(), Ct);
            await pm.RefundAsync(fixture.HouseholdId, sharedRefund, sharedExpense,
                20m, Guid.NewGuid(), Ct);
            await ThrowsAsync<BusinessRuleException>(() => pm.RefundAsync(
                fixture.HouseholdId, sharedRefund, sharedExpense, 20m, Guid.NewGuid(), Ct));
            await pm.SurrenderAsync(fixture.HouseholdId, 10m, On, Guid.NewGuid(), Ct);
            await ThrowsAsync<BusinessRuleException>(() => pm.SurrenderAsync(
                fixture.HouseholdId, 999999m, On, Guid.NewGuid(), Ct));
            var after = await ReadAsync(provider, fixture);
            Equal(baseline.Available - 50m, after.Available,
                "shared charge 60, shared refund 20, surrender 10");
            Equal(baseline.AttributedPrivateFunding, after.AttributedPrivateFunding,
                "all shared/surrender operations leave private funding unchanged");
            if (after.Charges.All(x => x.TransactionId != sharedExpense || x.Amount != 60m))
                throw new Exception("Shared charges must be editable.");
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var pm = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            await pm.ChargePrivateAsync(fixture.HouseholdId, privateExpense, 50m, Guid.NewGuid(), Ct);
            await pm.RefundAsync(fixture.HouseholdId, privateRefund, privateExpense,
                10m, Guid.NewGuid(), Ct);
        }
        var afterPrivate = await ReadAsync(provider, fixture);
        Equal(baseline.Available - 90m, afterPrivate.Available, "private consumption and refund");
        Equal(baseline.AttributedPrivateFunding - 40m, afterPrivate.AttributedPrivateFunding,
            "private expense/refund funding dimension");

        Guid fundingId, returnId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var transfers = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            fundingId = await transfers.CreateAsync(fixture.HouseholdId,
                fixture.HouseholdAccountId, fixture.PrivateAccountId,
                new TransferInput(100m, 100m, On, null), Ct, "FundExistingGrant");
            returnId = await transfers.CreateAsync(fixture.HouseholdId,
                fixture.PrivateAccountId, fixture.HouseholdAccountId,
                new TransferInput(40m, 40m, On, null), Ct, "ReturnFunding");
            var position = await ReadAsync(provider, fixture);
            Equal(afterPrivate.Available, position.Available, "return cannot surrender entitlement");
            Equal(afterPrivate.AttributedPrivateFunding + 60m,
                position.AttributedPrivateFunding, "funding minus return");
            var detail = await transfers.GetAsync(fixture.HouseholdId, returnId, Ct);
            await transfers.UpdateAsync(fixture.HouseholdId, returnId,
                new TransferInput(50m, 50m, On, null), detail.UpdatedAt, Ct);
            var updated = await ReadAsync(provider, fixture);
            Equal(afterPrivate.AttributedPrivateFunding + 50m,
                updated.AttributedPrivateFunding, "increasing a funding return");
            var current = await transfers.GetAsync(fixture.HouseholdId, returnId, Ct);
            await transfers.VoidAsync(fixture.HouseholdId, returnId, current.UpdatedAt, Ct);
            var voided = await ReadAsync(provider, fixture);
            Equal(afterPrivate.AttributedPrivateFunding + 100m,
                voided.AttributedPrivateFunding, "voiding a return restores attributed funding");
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var report = scope.ServiceProvider.GetRequiredService<BoundaryCashFlowHandler>();
            var months = await report.GetAsync(fixture.HouseholdId, On, On, Ct);
            var eur = months.Single(x => x.Currency == "EUR" && x.Month == new DateOnly(2026, 10, 1));
            if (eur.PayoutsToExcludedAccounts < 100m)
                throw new Exception("Boundary report must include funding payouts independently of wallet charges.");
            if (eur.ReturnsToHousehold != 0m)
                throw new Exception("Voided funding returns must not count as household inflow.");
        }

        user.UserId = fixture.MemberUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var pm = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            if (await pm.GetMineAsync(fixture.HouseholdId, Ct) is not null)
                throw new Exception("Other member must not see linked private/shared wallet charges.");
            await ThrowsAsync<BusinessRuleException>(() => pm.RefundAsync(
                fixture.HouseholdId, privateRefund, privateExpense, 10m, Guid.NewGuid(), Ct));
        }
        user.UserId = fixture.OwnerUserId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            if (await AggregateFingerprintAsync(db) != aggregateBefore)
                throw new Exception("Wallet attribution must never alter categorized aggregates.");
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var refund = await db.Transactions.SingleAsync(x => x.Id == sharedRefund, Ct);
            refund.Void(clock.GetUtcNow());
            await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>()
                .ReverseTransactionChargeAsync(fixture.HouseholdId, sharedRefund, Ct);
            await db.SaveChangesAsync(Ct);
        }
        var afterRefundVoid = await ReadAsync(provider, fixture);
        Equal(baseline.Available - 110m, afterRefundVoid.Available,
            "voided shared refund reverses entitlement only");
        Equal(afterPrivate.AttributedPrivateFunding + 100m,
            afterRefundVoid.AttributedPrivateFunding,
            "voided shared refund does not touch private funding");

        // Clean up test funding so no later tests see the newly posted transfer.
        await using (var scope = provider.CreateAsyncScope())
        {
            var transfers = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var detail = await transfers.GetAsync(fixture.HouseholdId, fundingId, Ct);
            await transfers.VoidAsync(fixture.HouseholdId, fundingId, detail.UpdatedAt, Ct);
        }
        Console.WriteLine("PASS personal-money reconciliation: shared/private splits, refunds, returns, surrender and boundary privacy");
    }

    private static async Task<Averio.Application.PersonalMoney.PersonalMoneySnapshot> ReadAsync(
        IServiceProvider provider, Fixture f)
    {
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
        var snapshot = await handler.GetMineAsync(f.HouseholdId, Ct)
            ?? throw new Exception("Missing wallet.");
        var summary = await handler.GetMySummaryAsync(f.HouseholdId, Ct)
            ?? throw new Exception("Missing summary.");
        if (summary.Available != snapshot.Available
            || summary.AttributedPrivateFunding != snapshot.AttributedPrivateFunding
            || summary.FundingDifference != snapshot.FundingDifference)
            throw new Exception("Summary must match reconciled entitlement and funding.");
        return snapshot;
    }

    private static async Task<string> AggregateFingerprintAsync(AverioDbContext db)
    {
        var rows = await db.TransactionAggregates.AsNoTracking().ToListAsync(Ct);
        return System.Text.Json.JsonSerializer.Serialize(rows.OrderBy(x =>
            System.Text.Json.JsonSerializer.Serialize(x)));
    }

    private static async Task ThrowsAsync<T>(Func<Task> callback) where T : Exception
    {
        try { await callback(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }

    private static void Equal(decimal expected, decimal actual, string meaning)
    {
        if (expected != actual) throw new Exception($"{meaning}: expected {expected}, got {actual}.");
    }
}
