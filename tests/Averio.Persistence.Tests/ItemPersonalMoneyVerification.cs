using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.PersonalMoney;
using Averio.Application.Transactions.ArchiveTransaction;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.GetTransaction;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Application.Transactions.Common;
using Averio.Domain.Categories;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class ItemPersonalMoneyVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly On = new(2026, 10, 20);

    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user,
        Fixture fixture)
    {
        user.UserId = fixture.OwnerUserId;
        Guid expenseCategory;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            expenseCategory = await db.Categories.AsNoTracking()
                .Where(c => c.IsBase && !c.IsArchived && c.Kind == CategoryKind.Expense)
                .Select(c => c.Id).FirstAsync(Ct);
        }
        decimal before;
        await using (var scope = provider.CreateAsyncScope())
        {
            before = (await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>()
                .GetMineAsync(fixture.HouseholdId, Ct))!.Available;
        }
        Guid id;
        await using (var scope = provider.CreateAsyncScope())
        {
            id = await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>()
                .HandleAsync(new CreateTransactionCommand(fixture.HouseholdId,
                    fixture.HouseholdAccountId, TransactionKind.Expense,
                    TransactionVisibility.Household, "Shared receipt", null, On,
                    [new TransactionItemInput("Family groceries", expenseCategory, 70m),
                     new TransactionItemInput("Personal item", expenseCategory, 30m,
                         PersonalMoneyAmount: 20m)], []), Ct);
            var snapshot = (await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>()
                .GetMineAsync(fixture.HouseholdId, Ct))!;
            if (snapshot.Available != before - 20m
                || snapshot.Entries.Count(x => x.TransactionId == id
                    && x.TransactionItemId is not null && x.Amount == -20m) != 1)
                throw new Exception("Mixed shared receipt failed to post only the item attribution.");
        }

        user.UserId = fixture.MemberUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var read = await scope.ServiceProvider.GetRequiredService<GetTransactionHandler>()
                .HandleAsync(fixture.HouseholdId, id, Ct);
            if (read.Items.Count != 2 || read.Amount != 100m)
                throw new Exception("Shared receipt must remain visible and financially intact.");
            var otherWallet = await scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>()
                .GetMineAsync(fixture.HouseholdId, Ct);
            if (otherWallet?.Entries.Any(x => x.TransactionId == id) == true)
                throw new Exception("Another member accessed the owner's private allowance attribution.");
            var archive = scope.ServiceProvider.GetRequiredService<ArchiveTransactionHandler>();
            await archive.HandleAsync(fixture.HouseholdId, id, true, read.UpdatedAt, Ct);
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var normal = await repository.GetVisiblePageAsync(fixture.HouseholdId,
                fixture.MemberId, new TransactionQuery(null,null,null,null,null,50,null,null,null), Ct);
            var archived = await repository.GetVisiblePageAsync(fixture.HouseholdId,
                fixture.MemberId, new TransactionQuery(null,null,null,null,null,50,null,null,null,
                    IncludeArchived:true), Ct);
            if (normal.Items.Any(x => x.Id == id) || !archived.Items.Any(x => x.Id == id)
                || !await db.Transactions.AnyAsync(x => x.Id == id && x.IsArchived, Ct))
                throw new Exception("Archive must hide normal overview only.");
            var version = (await db.Transactions.AsNoTracking()
                .SingleAsync(x => x.Id == id, Ct)).UpdatedAt;
            await archive.HandleAsync(fixture.HouseholdId, id, false, version, Ct);
            // Another household member may change shared merchant metadata; do not modify
            // the owner's private Personal Money item allocation.
            await UpdateAsync(scope.ServiceProvider, fixture, id, expenseCategory,
                "Updated shared receipt", 30m, keepSecond: true);
        }

        user.UserId = fixture.OwnerUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var pm = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            if ((await pm.GetMineAsync(fixture.HouseholdId, Ct))!.Available != before - 20m)
                throw new Exception("Metadata edit or archive incorrectly reversed a private budget allocation.");
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var tx = await db.Transactions.AsNoTracking().Include(x => x.Items)
                .SingleAsync(x => x.Id == id, Ct);
            var charge = (await pm.GetMineAsync(fixture.HouseholdId, Ct))!.Entries
                .Single(x => x.TransactionId == id && x.TransactionItemId is not null);
            if (!tx.Items.Any(x => x.Id == charge.TransactionItemId))
                throw new Exception("Metadata edit did not preserve stable item identity.");
        }

        user.UserId = fixture.MemberUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            await FailsAsync<ConflictException>(() => UpdateAsync(scope.ServiceProvider, fixture,
                id, expenseCategory, "Attempted excessive reduction", 10m, keepSecond: true));
            await UpdateAsync(scope.ServiceProvider, fixture, id, expenseCategory,
                "Shared receipt with item removed", 0m, keepSecond: false);
        }
        user.UserId = fixture.OwnerUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var pm = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            if ((await pm.GetMineAsync(fixture.HouseholdId, Ct))!.Available != before)
                throw new Exception("Deleting a linked item did not restore its Personal Money charge.");
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var transaction = await db.Transactions.SingleAsync(x => x.Id == id, Ct);
            if (transaction.Amount != 70m)
                throw new Exception("Item removal did not update original bank expense.");
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>()
                .HandleAsync(fixture.HouseholdId, id, transaction.UpdatedAt, Ct);
            if ((await pm.GetMineAsync(fixture.HouseholdId, Ct))!.Available != before)
                throw new Exception("Voiding a corrected source double-restored entitlement.");
        }
        Console.WriteLine("PASS item-level allowance: mixed receipt, privacy, stable item IDs, archive, edit, deletion and void");
    }

    private static async Task UpdateAsync(IServiceProvider services, Fixture fixture, Guid id,
        Guid category, string merchant, decimal amount, bool keepSecond)
    {
        var db = services.GetRequiredService<AverioDbContext>();
        var tx = await db.Transactions.AsNoTracking().Include(x => x.Items)
            .SingleAsync(x => x.Id == id, Ct);
        var sorted = tx.Items.OrderBy(x => x.Position).ToArray();
        var input = new List<TransactionItemInput>
        {
            new(sorted[0].Description, category, 70m, sorted[0].Id)
        };
        if (keepSecond)
            input.Add(new TransactionItemInput(sorted[1].Description, category, amount,
                sorted[1].Id));
        await services.GetRequiredService<UpdateTransactionHandler>()
            .HandleAsync(new UpdateTransactionCommand(fixture.HouseholdId, id,
                fixture.HouseholdAccountId, TransactionKind.Expense,
                TransactionVisibility.Household, merchant, null, On,
                input, [], tx.UpdatedAt), Ct);
    }

    private static async Task FailsAsync<T>(Func<Task> task) where T : Exception
    {
        try { await task(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
}
