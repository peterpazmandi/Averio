using Averio.Application.Activities;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.GetTransaction;
using Averio.Application.Transactions.GetTransactions;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Domain.Accounts;
using Averio.Domain.Activities;
using Averio.Domain.Common;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

internal static class ActivityVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        user.UserId = f.OwnerUserId;
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        Guid eur; Guid usd; Guid personal;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Account Account(string name, CurrencyCode currency, AccountScope kind) => Averio.Domain.Accounts.Account.Create(
                f.HouseholdId, [f.OwnerMemberId], name, AccountType.Checking, kind, AccountVisibility.Household,
                true, currency, 0m, day.AddDays(-10), clock.GetUtcNow());
            var a = Account("Activity EUR", CurrencyCode.Eur, AccountScope.Household);
            var b = Account("Activity USD", CurrencyCode.Usd, AccountScope.Household);
            var c = Account("Activity personal", CurrencyCode.Eur, AccountScope.Personal);
            db.Accounts.AddRange(a, b, c); await db.SaveChangesAsync();
            eur = a.Id; usd = b.Id; personal = c.Id;
        }
        var id = await Call(h => h.CreateAsync(f.HouseholdId, new("St. Ingbert", day, day.AddDays(3)), Ct));
        await Throws<ValidationException>(() => Call(h => h.CreateAsync(f.HouseholdId, new(" ", null, null), Ct)));
        await Throws<ValidationException>(() => Call(h => h.CreateAsync(f.HouseholdId, new("Dates", day, day.AddDays(-1)), Ct)));
        var expense = await Create(eur, TransactionKind.Expense, 575.1234m, day, id);
        var income = await Create(eur, TransactionKind.Income, 620m, day, id);
        var dollars = await Create(usd, TransactionKind.Expense, 12.3456m, day, id);
        var future = await Create(eur, TransactionKind.Income, 999m, day.AddDays(1), id);
        var hidden = await Create(personal, TransactionKind.Expense, 5m, day, id, TransactionVisibility.Private);
        var visiblePersonal = await Create(personal, TransactionKind.Expense, 7m, day, id);
        var totals = (await Read()).Totals;
        Check(totals.Single(x => x.Currency == "EUR").Net == 32.8766m, "Actual EUR net or future exclusion is wrong");
        Check(totals.Single(x => x.Currency == "USD").Net == -12.3456m, "Currencies were combined");
        Check(totals.Single(x => x.Currency == "EUR").TransactionCount == 4, "Count is incorrect");
        var before = await Detail(expense);
        Check(before.ActivityId == id && before.ActivityName == "St. Ingbert", "Detail assignment missing");
        await using (var scope = provider.CreateAsyncScope())
        {
            var page = await scope.ServiceProvider.GetRequiredService<GetTransactionsHandler>().HandleAsync(
                f.HouseholdId, null, null, null, null, null, 100, null, null, null, Ct, id);
            Check(page.Items.Count == 6 && page.Items.All(x => x.ActivityId == id), "Activity filter or metadata wrong");
        }
        user.UserId = f.MemberUserId;
        Check((await Call(h => h.ListAsync(f.HouseholdId, Ct))).Count == 0, "Other member sees activity");
        Check((await Detail(expense)).ActivityId is null, "Shared transaction leaks private assignment");
        await Throws<NotFoundException>(() => Assign(id, new ActivityAssignmentChange(expense, Guid.Empty)));
        await Throws<NotFoundException>(() => Assign(null, new ActivityAssignmentChange(hidden, Guid.Empty)));
        await Throws<NotFoundException>(() => Command(h => h.UpdateAsync(f.HouseholdId, id, new("Leak", null, null), before.UpdatedAt, Ct)));
        var memberActivity = await Call(h => h.CreateAsync(f.HouseholdId, new("Member activity", null, null), Ct));
        await Assign(memberActivity, new ActivityAssignmentChange(visiblePersonal, Guid.Empty));
        Check((await Detail(visiblePersonal)).ActivityId == memberActivity, "Independent member assignment failed");
        user.UserId = f.OwnerUserId;
        Check((await Detail(visiblePersonal)).ActivityId == id, "Member changed owner's assignment");
        // Removing account visibility must remove totals, list entries and inferred counts immediately.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = await db.Accounts.Include(x => x.Owners).SingleAsync(x => x.Id == personal);
            account.Update(account.Name, account.Type, [f.OwnerMemberId], AccountVisibility.Private, false, clock.GetUtcNow());
            await db.SaveChangesAsync();
        }
        user.UserId = f.MemberUserId;
        Check((await Call(h => h.ListAsync(f.HouseholdId, Ct))).Single().Totals.Count == 0, "Hidden account leaked through totals");
        await using (var scope = provider.CreateAsyncScope())
        {
            var page = await scope.ServiceProvider.GetRequiredService<GetTransactionsHandler>().HandleAsync(
                f.HouseholdId, null, null, null, null, null, 100, null, null, null, Ct, memberActivity);
            Check(page.Items.Count == 0, "Hidden account leaked through activity filter");
        }
        user.UserId = f.OwnerUserId;
        var activity = (await Read()).Activity;
        await Command(h => h.UpdateAsync(f.HouseholdId, id, new("Trip closed", null, null, true), activity.UpdatedAt, Ct));
        await Throws<ConflictException>(() => Command(h => h.UpdateAsync(f.HouseholdId, id, new("Stale", null, null), activity.UpdatedAt, Ct)));
        var unassigned = await Create(eur, TransactionKind.Expense, 2m, day, null);
        await Throws<ConflictException>(() => Assign(id, new ActivityAssignmentChange(unassigned, Guid.Empty)));
        // Failed inline assignment must roll back the new ledger row too.
        int countBefore;
        await using (var scope = provider.CreateAsyncScope())
            countBefore = await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Transactions.CountAsync();
        await Throws<ConflictException>(() => Create(eur, TransactionKind.Expense, 123m, day, id));
        await using (var scope = provider.CreateAsyncScope())
            Check(await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Transactions.CountAsync() == countBefore,
                "Failed inline assignment left a ledger entry");
        // An unchanged closed assignment remains legal during a ledger correction.
        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var detail = await services.GetRequiredService<GetTransactionHandler>().HandleAsync(f.HouseholdId, expense, Ct);
            await services.GetRequiredService<UpdateTransactionHandler>().HandleAsync(new(
                f.HouseholdId, expense, eur, TransactionKind.Expense, TransactionVisibility.Household,
                "Corrected", null, day, [new TransactionItemInput("Trip", TestIds.Groceries, 500m)], [], detail.UpdatedAt,
                new(id, detail.ActivityAssignmentVersion)), Ct);
        }
        Check((await Read()).Totals.Single(x => x.Currency == "EUR").Net == 108m, "Edit did not update result");
        await using (var scope = provider.CreateAsyncScope())
        {
            var d = await Detail(income);
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>().HandleAsync(f.HouseholdId, income, d.UpdatedAt, Ct);
        }
        Check((await Read()).Totals.Single(x => x.Currency == "EUR").Net == -512m, "Void still contributes");
        activity = (await Read()).Activity;
        await Command(h => h.UpdateAsync(f.HouseholdId, id, new("Trip reopened", day, null), activity.UpdatedAt, Ct));
        // Bulk rollback: first entry is valid, second is inaccessible/nonexistent.
        await Throws<NotFoundException>(() => Assign(id, new ActivityAssignmentChange(unassigned, Guid.Empty), new(Guid.NewGuid(), Guid.Empty)));
        Check((await Detail(unassigned)).ActivityId is null, "Partial bulk assignment committed");
        var ledgerVersion = (await Detail(unassigned)).UpdatedAt;
        await Assign(id, new ActivityAssignmentChange(unassigned, Guid.Empty));
        var linked = await Detail(unassigned);
        Check(linked.UpdatedAt == ledgerVersion, "Unexpected ledger version");
        await Throws<ConflictException>(() => Assign(null, new ActivityAssignmentChange(unassigned, Guid.Empty)));
        await Assign(null, new ActivityAssignmentChange(unassigned, linked.ActivityAssignmentVersion));
        var removed = await Detail(unassigned);
        Check(removed.ActivityId is null && removed.ActivityAssignmentVersion != Guid.Empty, "Unassigned version was lost");
        await Throws<ConflictException>(() => Assign(id, new ActivityAssignmentChange(unassigned, linked.ActivityAssignmentVersion)));
        // No real money or reporting change from assigning metadata.
        Check(removed.UpdatedAt == linked.UpdatedAt, "Assignment changed ledger timestamp");
        await Throws<BusinessRuleException>(() => Assign(id, new ActivityAssignmentChange(expense, Guid.Empty), new(expense, Guid.Empty)));
        // Independent assignment versions serialize concurrent browser requests.
        var requests = Enumerable.Range(0, 2).Select(async _ =>
        {
            try { await Assign(id, new ActivityAssignmentChange(unassigned, removed.ActivityAssignmentVersion)); return true; }
            catch (ConflictException) { return false; }
        });
        Check((await Task.WhenAll(requests)).Count(x => x) == 1, "Concurrent assignment failed to reject stale write");
        // Database FK rejects crossing activity owner boundaries.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            await Throws<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE activity_assignments SET activity_id = {memberActivity} WHERE household_member_id = {f.OwnerMemberId} AND transaction_id = {expense}"));
        }
        Console.WriteLine("PASS activity persistence, privacy, totals, atomic bulk assignment and concurrency");

        async Task<T> Call<T>(Func<ActivityHandler, Task<T>> action)
        {
            await using var scope = provider.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<ActivityHandler>());
        }
        async Task Command(Func<ActivityHandler, Task> action)
        {
            await using var scope = provider.CreateAsyncScope();
            await action(scope.ServiceProvider.GetRequiredService<ActivityHandler>());
        }
        Task Assign(Guid? target, params ActivityAssignmentChange[] changes) => Command(h => h.AssignAsync(f.HouseholdId, target, changes, Ct));
        async Task<ActivityResult> Read() => (await Call(h => h.ListAsync(f.HouseholdId, Ct))).Single(x => x.Activity.Id == id);
        async Task<TransactionDetailResult> Detail(Guid transactionId)
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<GetTransactionHandler>().HandleAsync(f.HouseholdId, transactionId, Ct);
        }
        async Task<Guid> Create(Guid accountId, TransactionKind kind, decimal amount, DateOnly on, Guid? activityId,
            TransactionVisibility visibility = TransactionVisibility.Household)
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(new(
                f.HouseholdId, accountId, kind, visibility, "Activity test", null, on,
                [new TransactionItemInput("Trip", kind == TransactionKind.Income ? TestIds.Salary : TestIds.Groceries, amount)], [],
                activityId is null ? null : new(activityId, Guid.Empty)), Ct);
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }
}
