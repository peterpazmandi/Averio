using Averio.Application.PersonalMoney;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transfers;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Domain.Common;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class PersonalMoneyVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly On = new(2026, 9, 21);

    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser currentUser,
        Fixture fixture, MutableTimeProvider clock)
    {
        currentUser.UserId = fixture.OwnerUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            var operationId = Guid.Parse("ed2ef98f-6d9d-41c6-bc41-1e716c9b4c6b");
            await handler.GrantAsync(fixture.HouseholdId, fixture.OwnerMemberId,
                "EUR", 300m, On, operationId, Ct);
            await handler.GrantAsync(fixture.HouseholdId, fixture.OwnerMemberId,
                "EUR", 300m, On, operationId, Ct);
            await FailureAsync<ConflictException>(() => handler.GrantAsync(fixture.HouseholdId,
                fixture.OwnerMemberId, "EUR", 301m, On, operationId, Ct));
            await BalanceAsync(handler, fixture.HouseholdId, 300m, 0m);
        }

        Guid fundingTransfer;
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            fundingTransfer = await handler.CreateAsync(fixture.HouseholdId,
                fixture.HouseholdAccountId, fixture.PrivateAccountId,
                new TransferInput(200m, 200m, On, null), Ct, "FundExistingGrant");
            await BalanceAsync(scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>(),
                fixture.HouseholdId, 300m, 200m);
            await FailureAsync<BusinessRuleException>(() => handler.CreateAsync(fixture.HouseholdId,
                fixture.HouseholdAccountId, fixture.PrivateAccountId,
                new TransferInput(101m, 101m, On, null), Ct, "FundExistingGrant"));
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var detail = await handler.GetAsync(fixture.HouseholdId, fundingTransfer, Ct);
            await handler.UpdateAsync(fixture.HouseholdId, fundingTransfer,
                new TransferInput(150m, 150m, On, null), detail.UpdatedAt, Ct);
            await BalanceAsync(scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>(),
                fixture.HouseholdId, 300m, 150m);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var detail = await handler.GetAsync(fixture.HouseholdId, fundingTransfer, Ct);
            await handler.VoidAsync(fixture.HouseholdId, fundingTransfer, detail.UpdatedAt, Ct);
            await BalanceAsync(scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>(),
                fixture.HouseholdId, 300m, 0m);
        }

        Guid combinedTransfer;
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            combinedTransfer = await handler.CreateAsync(fixture.HouseholdId,
                fixture.HouseholdAccountId, fixture.PrivateAccountId,
                new TransferInput(100m, 100m, On, null), Ct, "CreateAndFundGrant");
            await BalanceAsync(scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>(),
                fixture.HouseholdId, 400m, 100m);
        }

        Guid privateExpense;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var category = await db.Categories.OrderBy(x => x.Id).FirstAsync(Ct);
            var record = Transaction.Create(fixture.HouseholdId, fixture.PrivateAccountId,
                fixture.OwnerMemberId, TransactionKind.Expense, TransactionVisibility.Private,
                CurrencyCode.Eur, "Sensitive private purchase", "private details", On,
                [new TransactionItemDraft("Sensitive private item", category.Id,
                    "personal", "lock", 25m)], [], clock.GetUtcNow());
            db.Transactions.Add(record);
            await db.SaveChangesAsync(Ct);
            privateExpense = record.Id;
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            // Unlinked private expenses do not consume entitlement.
            await BalanceAsync(handler, fixture.HouseholdId, 400m, 100m);
            await handler.ChargePrivateAsync(fixture.HouseholdId, privateExpense, 25m, Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 375m, 75m);
            await FailureAsync<ConflictException>(() => handler.ChargePrivateAsync(
                fixture.HouseholdId, privateExpense, 25m, Ct));
        }

        // Corrections append deltas, preserve the bank transaction, and reject stale intent.
        var correctionId = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            await handler.CorrectChargeAsync(fixture.HouseholdId, privateExpense, 10m, 25m, 1, correctionId, Ct);
            await handler.CorrectChargeAsync(fixture.HouseholdId, privateExpense, 10m, 25m, 1, correctionId, Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 390m, 90m);
            await FailureAsync<ConflictException>(() => handler.CorrectChargeAsync(
                fixture.HouseholdId, privateExpense, 9m, 25m, 1, correctionId, Ct));
            await FailureAsync<ConflictException>(() => handler.CorrectChargeAsync(
                fixture.HouseholdId, privateExpense, 5m, 20m, 1, correctionId, Ct));
            await FailureAsync<ConflictException>(() => handler.CorrectChargeAsync(
                fixture.HouseholdId, privateExpense, 20m, 25m, 1, Guid.NewGuid(), Ct));
            await FailureAsync<BusinessRuleException>(() => handler.CorrectChargeAsync(
                fixture.HouseholdId, privateExpense, 26m, 10m, 2, Guid.NewGuid(), Ct));
            await FailureAsync<BusinessRuleException>(() => handler.CorrectChargeAsync(
                fixture.HouseholdId, privateExpense, -1m, 10m, 2, Guid.NewGuid(), Ct));
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            if ((await db.Transactions.AsNoTracking().SingleAsync(x => x.Id == privateExpense, Ct)).Amount != 25m)
                throw new Exception("Allowance correction must not change bank spending.");
            var snapshot = await handler.GetMineAsync(fixture.HouseholdId, Ct);
            if (snapshot!.Charges.Single().Amount != 10m || snapshot.Charges.Single().Version != 2)
                throw new Exception("Active charge must expose net amount and source version.");
        }
        currentUser.UserId = fixture.MemberUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            await FailureAsync<NotFoundException>(() => handler.CorrectChargeAsync(
                fixture.HouseholdId, privateExpense, 0m, 10m, 2, Guid.NewGuid(), Ct));
        }
        currentUser.UserId = fixture.OwnerUserId;
        // Separate DbContexts compete for the same household lock. Exactly one version wins.
        async Task<bool> Compete(decimal amount)
        {
            await using var scope = provider.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>().CorrectChargeAsync(
                    fixture.HouseholdId, privateExpense, amount, 10m, 2, Guid.NewGuid(), Ct);
                return true;
            }
            catch (ConflictException) { return false; }
        }
        var results = await Task.WhenAll(Compete(12m), Compete(14m));
        if (results.Count(x => x) != 1) throw new Exception("Only one concurrent correction may win.");
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            var active = (await handler.GetMineAsync(fixture.HouseholdId, Ct))!.Charges.Single();
            var unlinkId = Guid.NewGuid();
            await handler.CorrectChargeAsync(fixture.HouseholdId, privateExpense, 0m,
                active.Amount, active.Version, unlinkId, Ct);
            await handler.CorrectChargeAsync(fixture.HouseholdId, privateExpense, 0m,
                active.Amount, active.Version, unlinkId, Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 400m, 100m);
            if ((await handler.GetMineAsync(fixture.HouseholdId, Ct))!.Charges.Count != 0)
                throw new Exception("Unlinked charge should leave active list, not history.");
            await handler.ChargePrivateAsync(fixture.HouseholdId, privateExpense, 25m, Ct);
            // An old retry must not unlink a later attribution again.
            await handler.CorrectChargeAsync(fixture.HouseholdId, privateExpense, 0m,
                active.Amount, active.Version, unlinkId, Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 375m, 75m);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var version = (await db.Transactions.AsNoTracking()
                .SingleAsync(x => x.Id == privateExpense, Ct)).UpdatedAt;
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>()
                .HandleAsync(fixture.HouseholdId, privateExpense, version, Ct);
            await BalanceAsync(scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>(),
                fixture.HouseholdId, 400m, 100m);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var detail = await handler.GetAsync(fixture.HouseholdId, combinedTransfer, Ct);
            await handler.VoidAsync(fixture.HouseholdId, combinedTransfer, detail.UpdatedAt, Ct);
            await BalanceAsync(scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>(),
                fixture.HouseholdId, 300m, 0m);
        }

        currentUser.UserId = fixture.MemberUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var personal = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            if (await personal.GetMineAsync(fixture.HouseholdId, Ct) is not null)
                throw new Exception("Another member must not see the private wallet.");
            await FailureAsync<ForbiddenException>(() => personal.GrantAsync(
                fixture.HouseholdId, fixture.MemberId, "EUR", 300m, On, Guid.NewGuid(), Ct));
        }
        currentUser.UserId = fixture.OwnerUserId;
        Console.WriteLine("PASS personal money: private wallet, funded/existing grants, corrections, voids and owner authority");
    }

    private static async Task BalanceAsync(PersonalMoneyHandler handler, Guid householdId,
        decimal available, decimal funding)
    {
        var result = await handler.GetMineAsync(householdId, Ct)
            ?? throw new Exception("Missing wallet.");
        if (result.Available != available || result.AttributedPrivateFunding != funding)
            throw new Exception($"Wrong personal balances {result.Available}/{result.AttributedPrivateFunding}; expected {available}/{funding}.");
    }

    private static async Task FailureAsync<T>(Func<Task> callback) where T : Exception
    {
        try { await callback(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
}
