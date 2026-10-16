using Averio.Application.Recurring;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.Common;
using Averio.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Categories;
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
        await using (var scope = provider.CreateAsyncScope())
        {
            var due = await scope.ServiceProvider.GetRequiredService<IRecurringRepository>().DueAsync(day, 100, Ct);
            Check(due.Any(x => x.RuleId == autoId) && due.All(x => x.RuleId != skipId && x.RuleId != id), "Due discovery included manual rules");
        }
        await Throws<NotFoundException>(() => Call(h => h.AutomaticAsync(candidate with { UserId = Guid.NewGuid() }, Ct)));
        var automatic = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Call(h => h.AutomaticAsync(candidate, Ct))));
        Check(automatic.Count(x => x) == 1, "Concurrent workers posted twice");
        var pausedId = await Call(h => h.SaveAsync(f.HouseholdId, null, null, input with { Automatic = true }, Ct));
        var paused = await Read(pausedId);
        await Call(h => h.SaveAsync(f.HouseholdId, pausedId, paused.Version, input with { Automatic = true, IsPaused = true }, Ct));
        Check(!await Call(h => h.AutomaticAsync(new(pausedId, f.HouseholdId, f.OwnerUserId, paused.Version), Ct)), "Stale worker ignored pause");
        await Templates();
        // References are checked again at execution, after a schedule was accepted.
        Guid categoryId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var category = Category.Create(f.HouseholdId, "Recurring temporary", CategoryKind.Expense, "🏠", "#10B981", null, clock.GetUtcNow());
            db.Categories.Add(category); await db.SaveChangesAsync(); categoryId = category.Id;
        }
        var categoryInput = input with { CategoryId = categoryId, Automatic = true };
        var categoryRuleId = await Call(h => h.SaveAsync(f.HouseholdId, null, null, categoryInput, Ct));
        var categoryRule = await Read(categoryRuleId);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            (await db.Categories.SingleAsync(x => x.Id == categoryId)).SetArchived(true, clock.GetUtcNow());
            await db.SaveChangesAsync();
        }
        await Throws<BusinessRuleException>(() => Call(h => h.AutomaticAsync(new(categoryRuleId, f.HouseholdId, f.OwnerUserId, categoryRule.Version), Ct)));
        Check((await Read(categoryRuleId)).NextDueOn == day, "Archived category consumed date");
        await Call(h => h.SaveAsync(f.HouseholdId, categoryRuleId, categoryRule.Version, categoryInput with { IsPaused = true }, Ct));
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
        await Recovery();
        await Fairness();
        Console.WriteLine("PASS recurring persistence, privacy, exact money, idempotency, worker races, pause and archive handling");


        async Task Templates()
        {
            Guid activityId, foreignActivityId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                var activity = Averio.Domain.Activities.Activity.Create(f.HouseholdId, f.OwnerMemberId, "Recurring activity", null, null, clock.GetUtcNow());
                var foreign = Averio.Domain.Activities.Activity.Create(f.HouseholdId, f.MemberId, "Other member activity", null, null, clock.GetUtcNow());
                db.AddRange(activity, foreign); await db.SaveChangesAsync();
                activityId = activity.Id; foreignActivityId = foreign.Id;
            }
            var template = input with {
                Name = "Itemized bill", Amount = 12.3456m, Merchant = "Provider", Note = "Template note",
                Items = [new("Service", TestIds.Groceries, 10.3456m), new("Extra", TestIds.Groceries, 2m)],
                LabelIds = [f.HouseholdLabelId], ActivityId = activityId
            };
            await Throws<NotFoundException>(() => Call(h => h.SaveAsync(f.HouseholdId, null, null, template with { ActivityId = foreignActivityId }, Ct)));
            await Throws<BusinessRuleException>(() => Call(h => h.SaveAsync(f.HouseholdId, null, null, template with { LabelIds = [f.PrivateLabelId] }, Ct)));
            var rid = await Call(h => h.SaveAsync(f.HouseholdId, null, null, template, Ct));
            var r = await Read(rid);
            Check(r.Items.Count == 2 && r.Labels.Count == 1 && r.ActivityId == activityId, "Template details missing");
            Check((await Call(h => h.HistoryAsync(f.HouseholdId, rid, Ct))).Count == 0, "Saving template posted immediately");
            await Throws<BusinessRuleException>(() => Call(h => h.ResolveAsync(f.HouseholdId, rid, r.Version, day, false, 9m, Ct)));
            await Throws<BusinessRuleException>(() => Call(h => h.ResolveAsync(f.HouseholdId, rid, r.Version, day, false, null, Ct, [1m])));
            var txId = await Call(h => h.ResolveAsync(f.HouseholdId, rid, r.Version, day, false, null, Ct, [15.1234m, 2m]));
            Check(await Call(h => h.ResolveAsync(f.HouseholdId, rid, r.Version, day, false, null, Ct, [15.1234m, 2m])) == txId, "Itemized retry duplicated transaction");
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                var tx = await db.Transactions.Include(x => x.Items).Include(x => x.Labels).SingleAsync(x => x.Id == txId);
                Check(tx.Amount == 17.1234m && tx.Merchant == "Provider" && tx.Note == "Template note", "Posted details or exact total lost");
                Check(tx.Items.OrderBy(i => i.Position).Select(i => i.Description).SequenceEqual(["Service", "Extra"]), "Item descriptions/order lost");
                Check(tx.Labels.Single().LabelId == f.HouseholdLabelId, "Labels not repeated");
                Check(await db.Set<Averio.Domain.Activities.ActivityAssignment>().AnyAsync(x => x.TransactionId == txId && x.ActivityId == activityId && x.HouseholdMemberId == f.OwnerMemberId), "Creator's activity not assigned");
            }
            r = await Read(rid);
            await Call(h => h.SaveAsync(f.HouseholdId, rid, r.Version, template with { Note = "Changed for future", Automatic = true }, Ct));
            r = await Read(rid);
            Check(r.Items.Count == 2 && r.Labels.Single().LabelId == f.HouseholdLabelId, "Editing lost items or retained label");
            await using (var scope = provider.CreateAsyncScope())
                Check((await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Transactions.SingleAsync(x => x.Id == txId)).Note == "Template note", "Schedule edit rewrote posted history");
            var autoId = await Call(h => h.SaveAsync(f.HouseholdId, null, null, template with { Automatic = true }, Ct));
            var automatic = await Read(autoId);
            Check(await Call(h => h.AutomaticAsync(new(autoId, f.HouseholdId, f.OwnerUserId, automatic.Version), Ct)), "Itemized automatic posting failed");
            var closedId = await Call(h => h.SaveAsync(f.HouseholdId, null, null, template with { Automatic = true }, Ct));
            var closed = await Read(closedId);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                var activity = await db.Set<Averio.Domain.Activities.Activity>().SingleAsync(x => x.Id == activityId);
                activity.Update(activity.Name, null, null, true, clock.GetUtcNow()); await db.SaveChangesAsync();
            }
            await Throws<ConflictException>(() => Call(h => h.AutomaticAsync(new(closedId, f.HouseholdId, f.OwnerUserId, closed.Version), Ct)));
            Check((await Call(h => h.HistoryAsync(f.HouseholdId, closedId, Ct))).Count == 0, "Closed activity failure consumed occurrence");
            await Call(h => h.SaveAsync(f.HouseholdId, closedId, closed.Version, template with { IsPaused = true }, Ct));
        }

        async Task Recovery()
        {
            Guid personal;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                var a = Account.Create(f.HouseholdId, [f.OwnerMemberId, f.MemberId], "Recovery", AccountType.Checking,
                    AccountScope.Personal, AccountVisibility.Household, true, CurrencyCode.Eur, 0, day, clock.GetUtcNow());
                db.Accounts.Add(a); await db.SaveChangesAsync(); personal = a.Id;
            }
            var original = input with { AccountId = personal };
            var rid = await Call(h => h.SaveAsync(f.HouseholdId, null, null, original, Ct));
            var r = await Read(rid);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                var a = await db.Accounts.Include(x => x.Owners).SingleAsync(x => x.Id == personal);
                a.Update(a.Name, a.Type, [f.OwnerMemberId, f.MemberId], AccountVisibility.Private, false, clock.GetUtcNow());
                await db.SaveChangesAsync();
            }
            await Call(h => h.SaveAsync(f.HouseholdId, rid, r.Version, original with { IsPaused = true }, Ct));
            r = await Read(rid);
            await Throws<BusinessRuleException>(() => Call(h => h.SaveAsync(f.HouseholdId, rid, r.Version, original, Ct)));
            await Call(h => h.SaveAsync(f.HouseholdId, rid, r.Version, original with { Visibility = TransactionVisibility.Private }, Ct));
            r = await Read(rid);
            Check(!r.IsPaused && r.Visibility == TransactionVisibility.Private, "Privacy recovery failed");
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                var a = await db.Accounts.Include(x => x.Owners).SingleAsync(x => x.Id == personal);
                a.Update(a.Name, a.Type, [f.MemberId], AccountVisibility.Household, true, clock.GetUtcNow());
                await db.SaveChangesAsync();
            }
            var privateInput = original with { Visibility = TransactionVisibility.Private };
            await Throws<ForbiddenException>(() => Call(h => h.SaveAsync(f.HouseholdId, rid, r.Version, privateInput, Ct)));
            await Call(h => h.SaveAsync(f.HouseholdId, rid, r.Version, privateInput with { IsPaused = true }, Ct));
            r = await Read(rid);
            user.UserId = f.MemberUserId;
            await Throws<NotFoundException>(() => Call(h => h.SaveAsync(f.HouseholdId, rid, r.Version, privateInput with { IsStopped = true }, Ct)));
            user.UserId = f.OwnerUserId;
            await Call(h => h.SaveAsync(f.HouseholdId, rid, r.Version, privateInput with { IsStopped = true }, Ct));
            Check((await Read(rid)).IsStopped, "Former holder cannot stop owned schedule");
        }

        async Task Fairness()
        {
            var due = day.AddDays(-3);
            Guid poisonAccount, healthyAccount, healthyRule;
            List<Guid> poisonRules = [];
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                var a = Account.Create(f.HouseholdId, [f.OwnerMemberId], "Overflow queue", AccountType.Checking,
                    AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Eur, 0, due, clock.GetUtcNow());
                var b = Account.Create(f.HouseholdId, [f.OwnerMemberId], "Healthy queue", AccountType.Checking,
                    AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Eur, 0, due, clock.GetUtcNow());
                db.Accounts.AddRange(a,b); await db.SaveChangesAsync(); poisonAccount=a.Id; healthyAccount=b.Id;
                for (var i=0;i<100;i++)
                {
                    var rule = RecurringRule.Create(f.HouseholdId, f.OwnerMemberId, a.Id, TestIds.Groceries, "Poison", 1,
                        TransactionKind.Expense, TransactionVisibility.Household, RecurringFrequency.Monthly, due, due, true, clock.GetUtcNow());
                    poisonRules.Add(rule.Id); db.Add(rule);
                }
                var healthy = RecurringRule.Create(f.HouseholdId, f.OwnerMemberId, b.Id, TestIds.Groceries, "Healthy", 1,
                    TransactionKind.Expense, TransactionVisibility.Household, RecurringFrequency.Monthly, due.AddDays(1), due.AddDays(1), true, clock.GetUtcNow());
                db.Add(healthy); healthyRule=healthy.Id; await db.SaveChangesAsync();
            }
            Guid overflow;
            await using (var scope = provider.CreateAsyncScope())
                overflow = await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(new(
                    f.HouseholdId, poisonAccount, TransactionKind.Expense, TransactionVisibility.Household, null, null, due,
                    [new TransactionItemInput("Capacity", TestIds.Groceries, 999999999999999.9999m)], []), Ct);
            var batch = new RecurringBatch(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<RecurringBatch>.Instance, clock);
            await batch.RunAsync(Ct); // All 100 postings overflow the persisted numeric aggregate and roll back.
            await using (var scope = provider.CreateAsyncScope())
                Check(!await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Set<RecurringOccurrence>()
                    .AnyAsync(x => poisonRules.Contains(x.RuleId) || x.RuleId == healthyRule), "Failed batch consumed dates");
            await batch.RunAsync(Ct);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                Check(await db.Set<RecurringOccurrence>().AnyAsync(x => x.RuleId == healthyRule), "Healthy 101st rule starved");
                var tx = await db.Transactions.SingleAsync(x => x.Id == overflow);
                await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>().HandleAsync(f.HouseholdId, overflow, tx.UpdatedAt, Ct);
            }
            await batch.RunAsync(Ct); // Wrap to the repaired earlier rules.
            await using (var scope = provider.CreateAsyncScope())
                Check(await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Set<RecurringOccurrence>()
                    .CountAsync(x => poisonRules.Contains(x.RuleId)) == 100, "Earlier failures were never retried after cursor wrap");
        }

        async Task<T> Call<T>(Func<RecurringHandler, Task<T>> action) { await using var scope = provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<RecurringHandler>()); }
        async Task Command(Func<RecurringHandler, Task> action) { await using var scope = provider.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<RecurringHandler>()); }
        async Task<RecurringRule> Read(Guid rid) => (await Call(h => h.ListAsync(f.HouseholdId, Ct))).Single(x => x.Rule.Id == rid).Rule;
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
