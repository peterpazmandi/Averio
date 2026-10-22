using Averio.Application.PersonalMoney;
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
        Console.WriteLine("PASS item refund lifecycle: source edits, receipt-wide caps, partner deletion/void, and legacy transitions");

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
        async Task<Guid> CreateAsync(TransactionKind kind, decimal amount, decimal? charge = null, bool second = false)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var categoryKind = kind == TransactionKind.Expense ? CategoryKind.Expense : CategoryKind.Income;
            var category = await db.Categories.Where(x => x.IsBase && !x.IsArchived && x.Kind == categoryKind)
                .Select(x => x.Id).FirstAsync();
            var items = new List<TransactionItemInput> { new("First", category, amount, PersonalMoneyAmount: charge) };
            if (second) items.Add(new("Second", category, amount, PersonalMoneyAmount: charge));
            return await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>()
                .HandleAsync(new CreateTransactionCommand(f.HouseholdId, f.HouseholdAccountId,
                    kind, TransactionVisibility.Household, "Refund lifecycle", null, On, items, []), Ct);
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
