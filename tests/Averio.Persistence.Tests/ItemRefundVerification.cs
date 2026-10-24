using Averio.Application.PersonalMoney;
using Averio.Application.Accounts.UpdateAccount;
using Averio.Application.Accounts.ArchiveAccount;
using Averio.Application.Accounts.RestoreAccount;
using Averio.Application.Transactions.ArchiveTransaction;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Accounts;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Domain.Categories;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class ItemRefundVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly On = new(2026, 10, 20);

    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f)
    {
        user.UserId = f.OwnerUserId;
        var baseline = await BalanceAsync();
        var purchase = await CreateAsync(TransactionKind.Expense, 20m, 10m, second: true);
        var refund = await CreateAsync(TransactionKind.Income, 15m);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var originalIds = await db.TransactionItems.Where(x => x.TransactionId == purchase)
                .OrderBy(x => x.Position).Select(x => x.Id).ToArrayAsync();
            await EditAsync(purchase, items => items.Reverse().ToArray());
            var reorderedIds = await db.TransactionItems.AsNoTracking().Where(x => x.TransactionId == purchase)
                .OrderBy(x => x.Position).Select(x => x.Id).ToArrayAsync();
            if (!originalIds.Reverse().SequenceEqual(reorderedIds))
                throw new Exception("Reordering must preserve item IDs and persist the requested order.");
            await EditAsync(purchase, items => items.Reverse().ToArray());
            foreach (var id in new[] { purchase, refund })
            {
                var tx = await db.Transactions.AsNoTracking().SingleAsync(x => x.Id == id);
                await scope.ServiceProvider.GetRequiredService<ArchiveTransactionHandler>()
                    .HandleAsync(f.HouseholdId, id, true, tx.UpdatedAt, Ct);
            }
        }
        await LinkRefundAsync();
        await ExpectBalanceAsync(baseline - 5m, "two item charges and one purchase refund");

        // A metadata edit preserves the refund; a financial edit retracts it.
        user.UserId = f.MemberUserId;
        await EditAsync(refund, items => items, "Renamed refund");
        user.UserId = f.OwnerUserId;
        await ExpectBalanceAsync(baseline - 5m, "refund metadata edit");
        user.UserId = f.MemberUserId;
        await EditAsync(refund, items => items.Select(x => x with { Amount = 14m }).ToArray());
        user.UserId = f.OwnerUserId;
        await ExpectBalanceAsync(baseline - 20m, "cross-member refund amount edit retracts wallet credit");

        // Re-link a smaller refund, then reduce one allocation while total coverage remains sufficient.
        await LinkRefundAsync(12m);
        await EditAsync(purchase, items => items.Select((x, i) => x with
            { PersonalMoneyAmount = i == 0 ? 5m : 10m }).ToArray());
        await ExpectBalanceAsync(baseline - 3m, "refund cap uses receipt total, not one item");

        // Replacing an item while retaining total own allocation must assess the
        // final state, not temporarily reverse a refund between the two stages.
        await EditAsync(purchase, items => [items[0] with { ExistingItemId = null, PersonalMoneyAmount = 5m }, items[1]]);
        await ExpectBalanceAsync(baseline - 3m, "replacement item retains final refund coverage");

        // Removing an unrelated, unallocated item must not retract a valid refund.
        await EditAsync(purchase, items => items.Append(new TransactionItemInput(
            "Unallocated", items[0].CategoryId, 5m)).ToArray());
        user.UserId = f.MemberUserId;
        await EditAsync(purchase, items => items.Take(2).ToArray());
        user.UserId = f.OwnerUserId;
        await ExpectBalanceAsync(baseline - 3m, "unrelated item removal preserves refund");

        // Removing a charged item crosses the refund cap: reverse the affected wallet's
        // refund attribution, but keep the real income transaction and remaining charge.
        user.UserId = f.MemberUserId;
        await EditAsync(purchase, items => items.Take(1).ToArray());
        user.UserId = f.OwnerUserId;
        await ExpectBalanceAsync(baseline - 5m, "partner item removal retracts excess refund");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var income = await db.Transactions.AsNoTracking().SingleAsync(x => x.Id == refund);
            if (income.Status != TransactionStatus.Posted || income.Amount != 14m)
                throw new Exception("Wallet reconciliation changed real refund income.");
        }
        user.UserId = f.MemberUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var tx = await db.Transactions.AsNoTracking().SingleAsync(x => x.Id == purchase);
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>()
                .HandleAsync(f.HouseholdId, purchase, tx.UpdatedAt, Ct);
        }
        user.UserId = f.OwnerUserId;
        await ExpectBalanceAsync(baseline, "void after refund correction does not double restore");

        var corrected = await CreateAsync(TransactionKind.Expense, 20m, 10m);
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>()
                .RefundAsync(f.HouseholdId, refund, corrected, 6m, Guid.NewGuid(), Ct);
        }
        await EditAsync(corrected, items => items.Select(x => x with { PersonalMoneyAmount = 2m }).ToArray());
        await ExpectBalanceAsync(baseline - 2m, "explicit reduction retracts excess refund");
        await EditAsync(corrected, items => items.Select(x => x with { PersonalMoneyAmount = 0m }).ToArray());
        await ExpectBalanceAsync(baseline, "explicit unlink restores only remaining charge");

        // Reversed history must not permanently prevent switching attribution modes.
        var legacy = await CreateAsync(TransactionKind.Expense, 20m);
        await using (var scope = provider.CreateAsyncScope())
        {
            var pm = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            await pm.ChargePrivateAsync(f.HouseholdId, legacy, 10m, Guid.NewGuid(), Ct);
            var charge = (await pm.GetMineAsync(f.HouseholdId, Ct))!.Charges.Single(x => x.TransactionId == legacy);
            await pm.CorrectChargeAsync(f.HouseholdId, legacy, 0m, charge.Amount,
                charge.Version, Guid.NewGuid(), Ct);
        }
        await EditAsync(legacy, items => items.Select(x => x with { PersonalMoneyAmount = 8m }).ToArray());
        await ExpectBalanceAsync(baseline - 8m, "unlinked legacy charge permits item attribution");
        await EditAsync(legacy, items => items.Select(x => x with { PersonalMoneyAmount = 0m }).ToArray());
        await using (var scope = provider.CreateAsyncScope())
        {
            var pm = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            await pm.ChargePrivateAsync(f.HouseholdId, legacy, 7m, Guid.NewGuid(), Ct);
            var charge = (await pm.GetMineAsync(f.HouseholdId, Ct))!.Charges.Single(x => x.TransactionId == legacy);
            await pm.CorrectChargeAsync(f.HouseholdId, legacy, 0m, charge.Amount,
                charge.Version, Guid.NewGuid(), Ct);
        }
        await ExpectBalanceAsync(baseline, "unlinked item history permits legacy correction");

        var privatePurchase = await CreateAsync(TransactionKind.Expense, 20m, 10m, personal: true);
        await ChangePrivateAccountVisibilityAsync(AccountVisibility.Household);
        try
        {
            try
            {
                await EditAsync(privatePurchase, items => items.Select(x => x with { PersonalMoneyAmount = 5m }).ToArray());
                throw new Exception("An ineligible account accepted a positive allocation.");
            }
            catch (BusinessRuleException) { }
            await ExpectBalanceAsync(baseline - 10m, "rejected allocation leaves balance unchanged");
            await EditAsync(privatePurchase, items => items.Select(x => x with { PersonalMoneyAmount = 0m }).ToArray());
            await ExpectBalanceAsync(baseline, "unlink after account sharing change");
            await using var scope = provider.CreateAsyncScope();
            var entries = (await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>()
                .GetMineAsync(f.HouseholdId, Ct))!.Entries.Where(x => x.TransactionId == privatePurchase).ToArray();
            if (entries.Length != 2 || entries.Any(x => x.Kind.ToString() != "PrivateCharge")
                || entries.Sum(x => x.Amount) != 0)
                throw new Exception("Unlink must reverse the original private charge classification.");
        }
        finally { await ChangePrivateAccountVisibilityAsync(AccountVisibility.Private); }
        var archivedPurchase = await CreateAsync(TransactionKind.Expense, 20m, 10m, second: true, personal: true);
        var archivedRefund = await CreateAsync(TransactionKind.Income, 5m, personal: true);
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>()
                .RefundAsync(f.HouseholdId, archivedRefund, archivedPurchase, 5m, Guid.NewGuid(), Ct);
            await scope.ServiceProvider.GetRequiredService<ArchiveAccountHandler>()
                .HandleAsync(f.HouseholdId, f.PrivateAccountId, Ct);
        }
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var before = await db.Transactions.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == archivedPurchase);
            async Task<string> AggregatesAsync() => string.Join("\n", (await db.TransactionAggregates.AsNoTracking().ToArrayAsync())
                .Select(x => System.Text.Json.JsonSerializer.Serialize(x)).Order());
            var aggregates = await AggregatesAsync();
            var pm = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            var charge = (await pm.GetMineAsync(f.HouseholdId, Ct))!.Charges.Single(x => x.TransactionId == archivedPurchase);
            var operation = Guid.NewGuid();
            try
            {
                await pm.CorrectChargeAsync(f.HouseholdId, archivedPurchase, 1m, charge.Amount, charge.Version, Guid.NewGuid(), Ct);
                throw new Exception("Whole-receipt correction accepted a positive item allocation.");
            }
            catch (BusinessRuleException) { }
            user.UserId = f.MemberUserId;
            try
            {
                await pm.CorrectChargeAsync(f.HouseholdId, archivedPurchase, 0m, charge.Amount, charge.Version, Guid.NewGuid(), Ct);
                throw new Exception("Another member removed a private item allocation.");
            }
            catch (NotFoundException) { }
            finally { user.UserId = f.OwnerUserId; }
            await pm.CorrectChargeAsync(f.HouseholdId, archivedPurchase, 0m, charge.Amount, charge.Version, operation, Ct);
            await pm.CorrectChargeAsync(f.HouseholdId, archivedPurchase, 0m, charge.Amount, charge.Version, operation, Ct);
            await ExpectBalanceAsync(baseline, "archived account item unlink and retry restore allowance exactly once");
            var after = await db.Transactions.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == archivedPurchase);
            if (after.Amount != before.Amount || after.Status != before.Status || after.UpdatedAt == before.UpdatedAt
                || !after.Items.Select(x => x.Id).Order().SequenceEqual(before.Items.Select(x => x.Id).Order())
                || aggregates != await AggregatesAsync()
                || (await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == f.PrivateAccountId)).Status != AccountStatus.Archived)
                throw new Exception("Unlink changed source finances, item identity, or account archive status.");
            var entries = (await pm.GetMineAsync(f.HouseholdId, Ct))!.Entries.Where(x => x.TransactionId == archivedPurchase);
            if (entries.GroupBy(x => x.TransactionItemId).Any(g => g.Sum(x => x.Amount) != 0))
                throw new Exception("Unlink left an item allocation active.");
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RestoreAccountHandler>().HandleAsync(f.HouseholdId, f.PrivateAccountId, Ct);
        }
        Console.WriteLine("PASS item refund lifecycle: source edits, receipt-wide caps, partner deletion/void, and legacy transitions");

        async Task ChangePrivateAccountVisibilityAsync(AccountVisibility visibility)
        {
            await using var scope = provider.CreateAsyncScope();
            var account = await scope.ServiceProvider.GetRequiredService<AverioDbContext>()
                .Accounts.AsNoTracking().Include(x => x.Owners).SingleAsync(x => x.Id == f.PrivateAccountId);
            await scope.ServiceProvider.GetRequiredService<UpdateAccountHandler>().HandleAsync(
                new UpdateAccountCommand(f.HouseholdId, account.Id, account.Name, account.Type,
                    account.Owners.Select(x => x.HouseholdMemberId).ToArray(), visibility,
                    false, account.UpdatedAt), Ct);
        }

        async Task<decimal> BalanceAsync()
        {
            await using var scope = provider.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>()
                .GetMineAsync(f.HouseholdId, Ct))!.Available;
        }
        async Task ExpectBalanceAsync(decimal expected, string scenario)
        {
            var actual = await BalanceAsync();
            if (actual != expected) throw new Exception($"{scenario}: expected {expected}, got {actual}.");
        }
        async Task<Guid> CreateAsync(TransactionKind kind, decimal amount, decimal? charge = null, bool second = false, bool personal = false)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var categoryKind = kind == TransactionKind.Expense ? CategoryKind.Expense : CategoryKind.Income;
            var category = await db.Categories.Where(x => x.IsBase && !x.IsArchived && x.Kind == categoryKind)
                .Select(x => x.Id).FirstAsync();
            var items = new List<TransactionItemInput> { new("First", category, amount, PersonalMoneyAmount: charge) };
            if (second) items.Add(new("Second", category, amount, PersonalMoneyAmount: charge));
            return await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>()
                .HandleAsync(new CreateTransactionCommand(f.HouseholdId, personal ? f.PrivateAccountId : f.HouseholdAccountId,
                    kind, personal ? TransactionVisibility.Private : TransactionVisibility.Household, "Refund lifecycle", null, On, items, []), Ct);
        }
        async Task LinkRefundAsync(decimal amount = 15m)
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>()
                .RefundAsync(f.HouseholdId, refund, purchase, amount, Guid.NewGuid(), Ct);
        }
        async Task EditAsync(Guid id, Func<TransactionItemInput[], TransactionItemInput[]> transform,
            string? merchant = null)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var tx = await db.Transactions.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == id);
            var items = tx.Items.OrderBy(x => x.Position).Select(x => new TransactionItemInput(
                x.Description, x.CategoryId, x.Amount, x.Id)).ToArray();
            await scope.ServiceProvider.GetRequiredService<UpdateTransactionHandler>()
                .HandleAsync(new UpdateTransactionCommand(f.HouseholdId, id, tx.AccountId,
                    tx.Kind, tx.Visibility, merchant ?? tx.Merchant, tx.Note, tx.OccurredOn,
                    transform(items), [], tx.UpdatedAt), Ct);
        }
    }
}
