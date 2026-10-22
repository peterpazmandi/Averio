using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.PersonalMoney;
using Averio.Application.Accounts.UpdateAccount;
using Averio.Domain.Accounts;
using Averio.Domain.PersonalMoney;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transfers;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Application.Transactions.Common;
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

        await VerifyCreateAndChargeAsync(provider, fixture);

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

        var createChargeId = Guid.NewGuid();
        Guid privateExpense;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var category = await db.Categories.Where(x => x.IsBase && !x.IsArchived
                && x.Kind == Averio.Domain.Categories.CategoryKind.Expense).OrderBy(x => x.Id).FirstAsync(Ct);
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
            await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var concurrent = provider.CreateAsyncScope();
                await concurrent.ServiceProvider.GetRequiredService<PersonalMoneyHandler>().ChargePrivateAsync(
                    fixture.HouseholdId, privateExpense, 25m, createChargeId, Ct);
            }));
            await handler.ChargePrivateAsync(fixture.HouseholdId, privateExpense, 25m, createChargeId, Ct);
            await FailureAsync<ConflictException>(() => handler.ChargePrivateAsync(
                fixture.HouseholdId, privateExpense, 24m, createChargeId, Ct));
            await FailureAsync<BusinessRuleException>(() => handler.ChargePrivateAsync(
                fixture.HouseholdId, privateExpense, 25m, Guid.Empty, Ct));
            await BalanceAsync(handler, fixture.HouseholdId, 375m, 75m);
            await FailureAsync<ConflictException>(() => handler.ChargePrivateAsync(
                fixture.HouseholdId, privateExpense, 25m, Guid.NewGuid(), Ct));
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
            var description = (await db.Transactions.AsNoTracking().SingleAsync(x => x.Id == privateExpense, Ct)).Description;
            if (snapshot!.Entries.Where(x => x.TransactionId == privateExpense).Any(x => x.TransactionName != description))
                throw new Exception("Personal Money history must include the linked transaction name.");
            var names = await scope.ServiceProvider.GetRequiredService<IPersonalMoneyRepository>()
                .GetTransactionNamesAsync(fixture.HouseholdId, fixture.MemberId, [privateExpense], Ct);
            if (names.Count != 0) throw new Exception("History names must respect transaction visibility.");
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
            await SetPrivateAccountAsync(provider, fixture, AccountVisibility.Household, [fixture.OwnerMemberId]);
            await FailureAsync<BusinessRuleException>(() => handler.CorrectChargeAsync(fixture.HouseholdId,
                privateExpense, 1m, active.Amount, active.Version, Guid.NewGuid(), Ct));
            var unlinkId = Guid.NewGuid();
            await handler.CorrectChargeAsync(fixture.HouseholdId, privateExpense, 0m,
                active.Amount, active.Version, unlinkId, Ct);
            await handler.CorrectChargeAsync(fixture.HouseholdId, privateExpense, 0m,
                active.Amount, active.Version, unlinkId, Ct);
            await handler.ChargePrivateAsync(fixture.HouseholdId, privateExpense, 25m, createChargeId, Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 400m, 100m);
            if ((await handler.GetMineAsync(fixture.HouseholdId, Ct))!.Charges.Count != 0)
                throw new Exception("Unlinked charge should leave active list, not history.");
            await SetPrivateAccountAsync(provider, fixture, AccountVisibility.Private, [fixture.OwnerMemberId]);
            scope.ServiceProvider.GetRequiredService<AverioDbContext>().ChangeTracker.Clear();
            await handler.ChargePrivateAsync(fixture.HouseholdId, privateExpense, 25m, Guid.NewGuid(), Ct);
            // An old retry must not unlink a later attribution again.
            await handler.CorrectChargeAsync(fixture.HouseholdId, privateExpense, 0m,
                active.Amount, active.Version, unlinkId, Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 375m, 75m);
        }

        // Editing a corrected purchase retracts the net charge and invalidates its old version.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var record = await db.Transactions.AsNoTracking().Include(x => x.Items)
                .SingleAsync(x => x.Id == privateExpense, Ct);
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            var active = (await handler.GetMineAsync(fixture.HouseholdId, Ct))!.Charges.Single();
            await scope.ServiceProvider.GetRequiredService<UpdateTransactionHandler>().HandleAsync(
                new UpdateTransactionCommand(fixture.HouseholdId, privateExpense, record.AccountId,
                    TransactionKind.Expense, TransactionVisibility.Private, "Corrected private purchase", null, On.AddMonths(1),
                    [new TransactionItemInput("Updated private item", record.Items.First().CategoryId, 20m)],
                    [], record.UpdatedAt), Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 400m, 100m);
            await FailureAsync<ConflictException>(() => handler.CorrectChargeAsync(
                fixture.HouseholdId, privateExpense, 10m, active.Amount, active.Version, Guid.NewGuid(), Ct));
            await handler.ChargePrivateAsync(fixture.HouseholdId, privateExpense, 25m, createChargeId, Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 400m, 100m);
            await handler.ChargePrivateAsync(fixture.HouseholdId, privateExpense, 20m, Guid.NewGuid(), Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 380m, 80m);
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
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            await handler.ChargePrivateAsync(fixture.HouseholdId, privateExpense, 25m, createChargeId, Ct);
            await BalanceAsync(handler, fixture.HouseholdId, 400m, 100m);
            var ledger = await scope.ServiceProvider.GetRequiredService<AverioDbContext>()
                .Set<PersonalMoneyEntry>().AsNoTracking().Where(x => x.TransactionId == privateExpense).ToListAsync(Ct);
            if (ledger.GroupBy(x => x.OccurredOn).Any(g => g.Sum(x => x.Amount) != 0))
                throw new Exception("Source reversal must clear each date, not just the lifetime total.");
        }
        await VerifyTransferAuthorityAsync(provider, currentUser, fixture, combinedTransfer);

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
            if (await personal.GetMySummaryAsync(fixture.HouseholdId, Ct) is not null)
                throw new Exception("Another member must not see the private summary.");
            if (await personal.GetMineAsync(fixture.HouseholdId, Ct) is not null)
                throw new Exception("Another member must not see the private wallet.");
            await FailureAsync<ForbiddenException>(() => personal.GrantAsync(
                fixture.HouseholdId, fixture.MemberId, "EUR", 300m, On, Guid.NewGuid(), Ct));
        }
        currentUser.UserId = fixture.OwnerUserId;
        Console.WriteLine("PASS personal money: private wallet, funded/existing grants, corrections, voids and owner authority");
    }

    private static async Task VerifyCreateAndChargeAsync(IServiceProvider provider, Fixture fixture)
    {
        Guid categoryId;
        Guid incomeCategoryId;
        int count;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            categoryId = await db.Categories.Where(x => x.IsBase && !x.IsArchived
                && x.Kind == Averio.Domain.Categories.CategoryKind.Expense).Select(x => x.Id).FirstAsync(Ct);
            incomeCategoryId = await db.Categories.Where(x => x.IsBase && !x.IsArchived
                && x.Kind == Averio.Domain.Categories.CategoryKind.Income).Select(x => x.Id).FirstAsync(Ct);
            count = await db.Transactions.CountAsync(Ct);
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            await FailureAsync<BusinessRuleException>(() => scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>()
                .HandleAsync(new CreateTransactionCommand(fixture.HouseholdId, fixture.HouseholdAccountId,
                    TransactionKind.Income, TransactionVisibility.Household, "Rejected income allowance charge", null, On,
                    [new TransactionItemInput("Item", incomeCategoryId, 7m)], [], ChargePersonalMoney: true), Ct));
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            if (await db.Transactions.CountAsync(Ct) != count)
                throw new Exception("Rejected allowance attribution must roll back purchase creation.");
            var personal = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            await BalanceAsync(personal, fixture.HouseholdId, 300m, 0m);
            // Shared expenses are now eligible; the charge changes entitlement but not funding.
            var sharedId = await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>()
                .HandleAsync(new CreateTransactionCommand(fixture.HouseholdId, fixture.HouseholdAccountId,
                    TransactionKind.Expense, TransactionVisibility.Household, "Shared allowance purchase", null, On,
                    [new TransactionItemInput("Item", categoryId, 7m)], [], ChargePersonalMoney: true), Ct);
            await BalanceAsync(personal, fixture.HouseholdId, 293m, 0m);
            var sharedVersion = (await db.Transactions.AsNoTracking()
                .SingleAsync(x => x.Id == sharedId, Ct)).UpdatedAt;
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>()
                .HandleAsync(fixture.HouseholdId, sharedId, sharedVersion, Ct);
            await BalanceAsync(personal, fixture.HouseholdId, 300m, 0m);
            var id = await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>()
                .HandleAsync(new CreateTransactionCommand(fixture.HouseholdId, fixture.PrivateAccountId,
                    TransactionKind.Expense, TransactionVisibility.Private, "Allowance purchase", null, On,
                    [new TransactionItemInput("Item", categoryId, 7m)], [], ChargePersonalMoney: true), Ct);
            await BalanceAsync(personal, fixture.HouseholdId, 293m, -7m);
            var charge = (await personal.GetMineAsync(fixture.HouseholdId, Ct))!.Charges.Single();
            if (charge.TransactionId != id || charge.Amount != 7m) throw new Exception("Creation must charge the full purchase.");
            var version = (await db.Transactions.AsNoTracking().SingleAsync(x => x.Id == id, Ct)).UpdatedAt;
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>().HandleAsync(fixture.HouseholdId, id, version, Ct);
            await BalanceAsync(personal, fixture.HouseholdId, 300m, 0m);
        }
    }

    private static async Task SetPrivateAccountAsync(IServiceProvider provider, Fixture fixture,
        AccountVisibility visibility, IReadOnlyList<Guid> holders)
    {
        await using var scope = provider.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<AverioDbContext>()
            .Accounts.AsNoTracking().SingleAsync(x => x.Id == fixture.PrivateAccountId, Ct);
        await scope.ServiceProvider.GetRequiredService<UpdateAccountHandler>().HandleAsync(
            new UpdateAccountCommand(fixture.HouseholdId, account.Id, account.Name, account.Type,
                holders, visibility, false, account.UpdatedAt), Ct);
    }

    private static async Task VerifyTransferAuthorityAsync(IServiceProvider provider,
        MutableCurrentUser currentUser, Fixture fixture, Guid transferId)
    {
        await SetPrivateAccountAsync(provider, fixture, AccountVisibility.Private,
            [fixture.OwnerMemberId, fixture.MemberId]);
        currentUser.UserId = fixture.MemberUserId;
        // Each rejection gets its own request scope, just like the API.
        foreach (var voidTransfer in new[] { false, true })
        {
            await using var scope = provider.CreateAsyncScope();
            var transfers = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var value = await transfers.GetAsync(fixture.HouseholdId, transferId, Ct);
            await FailureAsync<ForbiddenException>(() => voidTransfer
                ? transfers.VoidAsync(fixture.HouseholdId, transferId, value.UpdatedAt, Ct)
                : transfers.UpdateAsync(fixture.HouseholdId, transferId,
                    new TransferInput(200m, 200m, On, null), value.UpdatedAt, Ct));
        }
        currentUser.UserId = fixture.OwnerUserId;
        foreach (var visibility in new[] { AccountVisibility.Private, AccountVisibility.Household })
        {
            await SetPrivateAccountAsync(provider, fixture, visibility,
                visibility == AccountVisibility.Private ? [fixture.OwnerMemberId, fixture.MemberId] : [fixture.OwnerMemberId]);
            await using var scope = provider.CreateAsyncScope();
            var transfers = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var value = await transfers.GetAsync(fixture.HouseholdId, transferId, Ct);
            await FailureAsync<BusinessRuleException>(() => transfers.UpdateAsync(fixture.HouseholdId,
                transferId, new TransferInput(200m, 200m, On, null), value.UpdatedAt, Ct));
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var transfers = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var value = await transfers.GetAsync(fixture.HouseholdId, transferId, Ct);
            if (value.Source!.Amount != 100m) throw new Exception("Rejected edit changed transfer amount.");
            await BalanceAsync(scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>(), fixture.HouseholdId, 400m, 100m);
            // Original wallet owner may clean up via void after the visibility change.
            await transfers.VoidAsync(fixture.HouseholdId, transferId, value.UpdatedAt, Ct);
            await BalanceAsync(scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>(), fixture.HouseholdId, 300m, 0m);
        }
        await SetPrivateAccountAsync(provider, fixture, AccountVisibility.Private, [fixture.OwnerMemberId]);
    }

    private static async Task BalanceAsync(PersonalMoneyHandler handler, Guid householdId,
        decimal available, decimal funding)
    {
        var result = await handler.GetMineAsync(householdId, Ct)
            ?? throw new Exception("Missing wallet.");
        var summary = await handler.GetMySummaryAsync(householdId, Ct)
            ?? throw new Exception("Missing summary.");
        if (summary.WalletId != result.WalletId || summary.Currency != result.Currency
            || summary.Available != result.Available
            || summary.AttributedPrivateFunding != result.AttributedPrivateFunding
            || summary.FundingDifference != result.FundingDifference)
            throw new Exception("Database summary must match the private ledger projection.");
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
