using Averio.Application.Recurring;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class RecurringVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        user.UserId = f.OwnerUserId;
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        Guid accountId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = Account.Create(f.HouseholdId, [f.OwnerMemberId], "Recurring verification", AccountType.Checking,
                AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Eur, 0, day, clock.GetUtcNow());
            db.Accounts.Add(account); await db.SaveChangesAsync(); accountId = account.Id;
        }
        var input = new RecurringInput(accountId, TestIds.Groceries, "Recurring bill", 12.3456m, TransactionKind.Expense,
            TransactionVisibility.Household, RecurringFrequency.Monthly, day, null, false);
        var id = await Call(h => h.SaveAsync(f.HouseholdId, null, null, input, Ct));
        var rule = await Read(id);
        Check(rule.NextDueOn == day && !rule.Automatic, "Manual default or first date");
        user.UserId = f.MemberUserId;
        Check(!(await Call(h => h.ListAsync(f.HouseholdId, Ct))).Any(x => x.Rule.Id == id), "Private rule leaked");
        await Throws<NotFoundException>(() => Call(h => h.ResolveAsync(f.HouseholdId, id, rule.Version, day, false, null, Ct)));
        user.UserId = f.OwnerUserId;
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Call(h => h.ResolveAsync(f.HouseholdId, id, rule.Version, day, false, 15.1234m, Ct))));
        Check(results.All(x => x == results[0]) && results[0] is not null, "Duplicate posting");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var tx = await db.Transactions.SingleAsync(x => x.Id == results[0]);
            Check(tx.Amount == 15.1234m && tx.OccurredOn == day, "Exact amount or due date lost");
            Check(await db.Set<RecurringOccurrence>().CountAsync(x => x.RuleId == id) == 1, "Receipt duplicated");
            Check(await db.TransactionAggregates.Where(x => x.AccountId == accountId && x.DimensionType == TransactionAggregateDimension.Account && x.Period == TransactionAggregatePeriod.Day).SumAsync(x => x.ExpenseAmount) == 15.1234m, "Aggregate double counted");
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>().HandleAsync(f.HouseholdId, tx.Id, tx.UpdatedAt, Ct);
        }
        Check(await Call(h => h.ResolveAsync(f.HouseholdId, id, rule.Version, day, false, 15.1234m, Ct)) == results[0], "Void reopened occurrence");
        await Throws<ConflictException>(() => Call(h => h.ResolveAsync(f.HouseholdId, id, rule.Version, day, true, null, Ct)));
        await Throws<ConflictException>(() => Call(h => h.SaveAsync(f.HouseholdId, id, rule.Version, input, Ct)));
        rule = await Read(id);
        await Throws<ConflictException>(() => Call(h => h.ResolveAsync(f.HouseholdId, id, rule.Version, rule.NextDueOn!.Value, false, null, Ct)));
        Check((await Read(id)).NextDueOn == day.AddMonths(1), "Future occurrence advanced");
        var skipId = await Call(h => h.SaveAsync(f.HouseholdId, null, null, input with { Name = "Skip bill" }, Ct));
        var skipRule = await Read(skipId);
        Check(await Call(h => h.ResolveAsync(f.HouseholdId, skipId, skipRule.Version, day, true, null, Ct)) is null, "Skip wrote transaction");
        Check(await Call(h => h.ResolveAsync(f.HouseholdId, skipId, skipRule.Version, day, true, null, Ct)) is null, "Skip retry failed");
        await Throws<ConflictException>(() => Call(h => h.ResolveAsync(f.HouseholdId, skipId, skipRule.Version, day, false, null, Ct)));
        var autoId = await Call(h => h.SaveAsync(f.HouseholdId, null, null, input with { Name = "Automatic bill", Automatic = true }, Ct));
        var auto = await Read(autoId);
        var candidate = new RecurringCandidate(autoId, f.HouseholdId, f.OwnerUserId, auto.Version);
        var automatic = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Call(h => h.AutomaticAsync(candidate, Ct))));
        Check(automatic.Count(x => x) == 1, "Concurrent workers posted twice");
        var pausedId = await Call(h => h.SaveAsync(f.HouseholdId, null, null, input with { Automatic = true }, Ct));
        var paused = await Read(pausedId);
        await Call(h => h.SaveAsync(f.HouseholdId, pausedId, paused.Version, input with { Automatic = true, IsPaused = true }, Ct));
        Check(!await Call(h => h.AutomaticAsync(new(pausedId, f.HouseholdId, f.OwnerUserId, paused.Version), Ct)), "Stale worker ignored pause");
        var blockedId = await Call(h => h.SaveAsync(f.HouseholdId, null, null, input with { Automatic = true }, Ct));
        var blocked = await Read(blockedId);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var a = await db.Accounts.SingleAsync(x => x.Id == accountId); a.Archive(clock.GetUtcNow()); await db.SaveChangesAsync();
        }
        var blockedCandidate = new RecurringCandidate(blockedId, f.HouseholdId, f.OwnerUserId, blocked.Version);
        await Throws<ConflictException>(() => Call(h => h.AutomaticAsync(blockedCandidate, Ct)));
        await Command(h => h.BlockAsync(blockedCandidate, Ct));
        Check((await Read(blockedId)).IsBlocked, "Blocked schedule status missing");
        await using (var scope = provider.CreateAsyncScope())
            Check(!await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Set<RecurringOccurrence>().AnyAsync(x => x.RuleId == blockedId), "Failure consumed occurrence");
        var blockedVersion = (await Read(blockedId)).Version;
        await Call(h => h.SaveAsync(f.HouseholdId, blockedId, blockedVersion, input with { IsStopped = true }, Ct));
        Check((await Read(blockedId)).IsStopped, "Cannot stop unavailable schedule");
        await Throws<BusinessRuleException>(() => Call(h => h.SaveAsync(f.HouseholdId, null, null, input with { StartsOn = day.AddDays(-1) }, Ct)));
        Console.WriteLine("PASS recurring persistence, privacy, exact money, idempotency, worker races, pause and archive handling");

        async Task<T> Call<T>(Func<RecurringHandler, Task<T>> action) { await using var scope = provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<RecurringHandler>()); }
        async Task Command(Func<RecurringHandler, Task> action) { await using var scope = provider.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<RecurringHandler>()); }
        async Task<RecurringRule> Read(Guid rid) => (await Call(h => h.ListAsync(f.HouseholdId, Ct))).Single(x => x.Rule.Id == rid).Rule;
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
