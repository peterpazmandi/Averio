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

internal static class ReimbursementVerification
{
    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        var ct = CancellationToken.None;
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        user.UserId = f.OwnerUserId;
        Guid accountId, usdId, viewOnlyId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = Account.Create(f.HouseholdId, [f.OwnerMemberId], "Mortgage test", AccountType.Checking,
                AccountScope.Personal, AccountVisibility.Private, false, CurrencyCode.Eur, 0, day, clock.GetUtcNow());
            var usd = Account.Create(f.HouseholdId, [f.OwnerMemberId], "USD destination", AccountType.Checking,
                AccountScope.Personal, AccountVisibility.Private, false, CurrencyCode.Usd, 0, day, clock.GetUtcNow());
            var viewOnly = Account.Create(f.HouseholdId, [f.MemberId], "View-only destination", AccountType.Checking,
                AccountScope.Personal, AccountVisibility.Household, true, CurrencyCode.Eur, 0, day, clock.GetUtcNow());
            db.AddRange(account, usd, viewOnly); await db.SaveChangesAsync(); accountId = account.Id; usdId = usd.Id; viewOnlyId = viewOnly.Id;
        }
        RecurringReimbursementRule Config(Guid destination, decimal amount = 30) => RecurringReimbursementRule.Create("Health fund", ReimbursementCalculation.Fixed, amount, null, destination, 7, 0);
        var input = new RecurringInput(accountId, TestIds.Groceries, "Mortgage", 150, TransactionKind.Expense,
            TransactionVisibility.Private, RecurringFrequency.Monthly, day, null, false, Reimbursement: Config(accountId));
        await Throws<BusinessRuleException>(() => Rule(h => h.SaveAsync(f.HouseholdId, null, null, input with { Reimbursement = Config(usdId) }, ct)));
        await Throws<BusinessRuleException>(() => Rule(h => h.SaveAsync(f.HouseholdId, null, null, input with { Reimbursement = Config(viewOnlyId) }, ct)));
        await Throws<BusinessRuleException>(() => Rule(h => h.SaveAsync(f.HouseholdId, null, null, input with { Kind = TransactionKind.Income }, ct)));
        await Throws<NotFoundException>(() => Rule(h => h.SaveAsync(f.HouseholdId, null, null, input with { Reimbursement = Config(Guid.NewGuid()) }, ct)));
        var id = await Rule(h => h.SaveAsync(f.HouseholdId, null, null, input, ct));
        Check((await Claims()).Count == 0, "Saving a rule created a claim");
        var rule = await ReadRule(id);
        var results = await Task.WhenAll(Enumerable.Range(0,3).Select(_ => Rule(h => h.ResolveAsync(f.HouseholdId, id, rule.Version, day, false, 120, ct))));
        Check(results.All(x => x == results[0]), "Concurrent recording duplicated payment");
        var claim = (await Claims()).Single().Claim;
        Check(claim.TransactionId == results[0] && claim.ExpectedAmount == 0 && claim.PotentialAmount == 30 && claim.SubmissionReminderOn == day, "Opportunity snapshot");
        var editedRule = await ReadRule(id);
        await Rule(h => h.SaveAsync(f.HouseholdId, id, editedRule.Version, input with { Reimbursement = Config(accountId, 50) }, ct));
        Check((await Claims()).Single().Claim.PotentialAmount == 30, "Rule update rewrote history");
        await ChangeThrows(false, day, claim.Version);
        await ChangeThrows(true, day.AddDays(1), claim.Version);
        await ChangeThrows(true, day.AddDays(-1), claim.Version);
        user.UserId = f.MemberUserId;
        Check((await Claims()).Count == 0, "Private claim leaked to member");
        await Throws<NotFoundException>(() => Change(true, day, claim.Version));
        user.UserId = f.OwnerUserId;
        var before = await Totals();
        await Change(true, day, claim.Version);
        var submitted = (await Claims()).Single().Claim;
        Check(submitted.ExpectedAmount == 30 && submitted.ExpectedOn == day.AddDays(7) && submitted.Events.Count == 2, "Submission not stored");
        Check(await Totals() == before, "Submission created or changed actual money");
        await Throws<ConflictException>(() => Change(true, day, claim.Version));
        await Change(false, null, submitted.Version, ReimbursementStatus.Rejected);
        var rejected = (await Claims()).Single().Claim;
        Check(rejected.ExpectedAmount == 0, "Rejected outstanding");
        await Change(true, day, rejected.Version);
        Check((await Claims()).Single().Claim.Events.Count == 4, "Resubmission lost history");
        // Skips never generate claims. The automatic path creates exactly one claim with its expense.
        var skipId = await Rule(h => h.SaveAsync(f.HouseholdId, null, null, input, ct));
        var skip = await ReadRule(skipId);
        await Rule(h => h.ResolveAsync(f.HouseholdId, skipId, skip.Version, day, true, null, ct));
        Check((await Claims()).Count == 1, "Skip generated claim");
        var autoId = await Rule(h => h.SaveAsync(f.HouseholdId, null, null, input with { Automatic = true }, ct));
        var auto = await ReadRule(autoId);
        var worker = await Task.WhenAll(Enumerable.Range(0,3).Select(_ => Rule(h => h.AutomaticAsync(new(autoId, f.HouseholdId, f.OwnerUserId, auto.Version), ct))));
        Check(worker.Count(x => x) == 1 && (await Claims()).Count == 2, "Worker duplicated claim");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var changedPayment = await db.Transactions.SingleAsync(x => x.Id == claim.TransactionId);
            db.Entry(changedPayment).Property(x => x.Kind).CurrentValue = TransactionKind.Income;
            await db.SaveChangesAsync();
        }
        Check((await Claims()).Single(c => c.Claim.Id == claim.Id).PaymentUnavailable, "Income-converted source remains eligible");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var payment = await db.Transactions.SingleAsync(x => x.Id == claim.TransactionId);
            db.Entry(payment).Property(x => x.Kind).CurrentValue = TransactionKind.Expense;
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>().HandleAsync(f.HouseholdId, payment.Id, payment.UpdatedAt, ct);
        }
        var voided = (await Claims()).Single(c => c.Claim.Id == claim.Id);
        Check(voided.PaymentUnavailable, "Void not reflected");
        await Change(false, null, voided.Claim.Version, ReimbursementStatus.Cancelled);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check(await db.Set<ReimbursementClaim>().CountAsync() == 2, "History was deleted");
            Check(!await db.Transactions.AnyAsync(t => t.AccountId == accountId && t.Kind == TransactionKind.Income), "Automatic income created");
            var a = await db.Accounts.Include(a => a.Owners).SingleAsync(a => a.Id == accountId);
            a.Update(a.Name, a.Type, [f.MemberId], AccountVisibility.Private, false, clock.GetUtcNow()); await db.SaveChangesAsync();
        }
        Check((await Claims()).Count == 0, "Lost account visibility leaked claim");
        Console.WriteLine("PASS reimbursement persistence, consent, snapshots, privacy, skips, concurrent workers, voiding and no automatic income");

        async Task<T> Rule<T>(Func<RecurringHandler,Task<T>> action) { await using var scope = provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<RecurringHandler>()); }
        async Task<RecurringRule> ReadRule(Guid rid) => (await Rule(h => h.ListAsync(f.HouseholdId, ct))).Single(x => x.Rule.Id == rid).Rule;
        async Task<IReadOnlyList<ReimbursementResult>> Claims() { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().ListAsync(f.HouseholdId, null, ct); }
        async Task Change(bool consent, DateOnly? date, Guid version, ReimbursementStatus status = ReimbursementStatus.Submitted) { await using var scope = provider.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().ChangeAsync(f.HouseholdId, claim.Id, version, consent, date, "ref", status, ct); }
        Task ChangeThrows(bool consent, DateOnly date, Guid version) => Throws<BusinessRuleException>(() => Change(consent, date, version));
        async Task<(int, decimal, decimal)> Totals() { await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>(); return (await db.Transactions.CountAsync(), await db.Transactions.SumAsync(t => t.Amount), await db.TransactionAggregates.SumAsync(a => a.IncomeAmount)); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T:Exception { try { await action(); } catch(T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}

