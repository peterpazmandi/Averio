using Averio.Application.Common.Exceptions;
using Averio.Application.Recurring;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class ManualReimbursementVerification
{
    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        var ct = CancellationToken.None;
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        user.UserId = f.OwnerUserId;
        var config = RecurringReimbursementRule.Create("Manual health fund", ReimbursementCalculation.Fixed, 30, null, f.HouseholdAccountId, 7, 0);
        CreateTransactionCommand Input(RecurringReimbursementRule? reimbursement = null, TransactionKind kind = TransactionKind.Expense) => new(f.HouseholdId, f.HouseholdAccountId,
            kind, TransactionVisibility.Household, "Manual reimbursement source", null, day,
            [new TransactionItemInput("Expense", kind == TransactionKind.Expense ? TestIds.Groceries : TestIds.Salary, 150)], [], Reimbursement: reimbursement);
        var atomic = await Post(Input(config));
        var initial = (await List(atomic)).Single().Claim;
        Check(initial.RuleId == null && initial.Status == ReimbursementStatus.NeedsSubmission && initial.ExpectedAmount == 0 && initial.PotentialAmount == 30, "Manual claim snapshot");
        var overview = await MoneyBack("NeedsSubmission");
        var initialRow = overview.Items.Single(x => x.Id == initial.Id);
        Check(initialRow.Description == "Manual reimbursement source" && initialRow.Amount == 30 && initialRow.Outstanding == 0, "Overview must show the potential estimate, not expected amount");
        await MoneyBackSnapshotVerification.RunAsync(provider, f, initial.Id);
        Check((await MoneyBack("Submitted")).Items.All(x => x.Id != initial.Id), "Overview status filter included an unsubmitted claim");
        var before = await Totals();
        await Throws<BusinessRuleException>(() => Post(Input(config, TransactionKind.Income)));
        Check(await Totals() == before, "Invalid income claim left a transaction behind");
        var tiny = RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Percentage, 0.0001m, null, f.HouseholdAccountId, 0, null);
        var tinyInput = Input(tiny) with { Items = [new TransactionItemInput("Small", TestIds.Groceries, 0.0001m)] };
        await Throws<BusinessRuleException>(() => Post(tinyInput));
        Check(await Totals() == before, "Zero estimate did not roll back");
        var oversized = RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Fixed, 150.0001m, null, f.HouseholdAccountId, 7, 0);
        await Throws<BusinessRuleException>(() => Post(Input(oversized)));
        Check(await Totals() == before, "Oversized claim did not roll back its expense");
        var expense = await Post(Input());
        var payment = await Payment(expense);
        before = await Totals();
        await Throws<BusinessRuleException>(() => Create(expense, payment.UpdatedAt, oversized));
        Check((await List(expense)).Count == 0 && await Totals() == before, "Oversized claim changed recorded finances");
        await Throws<ConflictException>(() => Create(expense, payment.UpdatedAt.AddSeconds(-1), config));
        var hidden = RecurringReimbursementRule.Create("Private destination", ReimbursementCalculation.Fixed, 30, null, f.PrivateAccountId, 7, 0);
        user.UserId = f.MemberUserId;
        await Throws<NotFoundException>(() => Create(expense, payment.UpdatedAt, hidden));
        Guid viewOnlyId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = Account.Create(f.HouseholdId, [f.OwnerMemberId], "Visible personal destination", AccountType.Checking,
                AccountScope.Personal, AccountVisibility.Household, true, CurrencyCode.Eur, 0, day, clock.GetUtcNow());
            db.Accounts.Add(account); await db.SaveChangesAsync(ct); viewOnlyId = account.Id;
        }
        var viewOnly = RecurringReimbursementRule.Create("View only", ReimbursementCalculation.Fixed, 30, null, viewOnlyId, 7, 0);
        await Throws<BusinessRuleException>(() => Create(expense, payment.UpdatedAt, viewOnly));
        await Throws<BusinessRuleException>(() => Post(Input(viewOnly)));
        user.UserId = f.OwnerUserId;
        var ids = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Create(expense, payment.UpdatedAt, config)));
        Check(ids.Distinct().Count() == 1, "Retry duplicated manual claim");
        Check(await Totals() == before, "Manual claim changed actual money");
        var claim = (await List(expense)).Single().Claim;
        Check(claim.RuleId == null && claim.OriginalPaymentAmount == payment.Amount && claim.Events.Count == 1, "Expense-linked manual claim");
        Check(await DetailStatus(expense) == "NeedsSubmission" && await OverviewStatus(expense) == "NeedsSubmission", "Owner reimbursement badge missing");
        var different = RecurringReimbursementRule.Create("Other", ReimbursementCalculation.Fixed, 40, null, f.HouseholdAccountId, 7, 0);
        await Throws<ConflictException>(() => Create(expense, payment.UpdatedAt, different));
        user.UserId = f.MemberUserId;
        Check((await List(expense)).Count == 0, "Shared expense leaked private claim");
        Check((await MoneyBack(null)).Items.All(x => x.Id != claim.Id && x.Id != initial.Id), "Money-back overview leaked another member's claim");
        Check(await DetailStatus(expense) == null && await OverviewStatus(expense) == null, "Shared expense badge leaked private claim");
        await Throws<ConflictException>(() => Create(expense, payment.UpdatedAt, config));
        await Throws<NotFoundException>(() => Create(Guid.NewGuid(), payment.UpdatedAt, config));
        var memberExpense = await Post(Input(config));
        var memberClaim = (await List(memberExpense)).Single().Claim;
        await Throws<BusinessRuleException>(() => Edit(memberClaim.Id, memberClaim.Version, viewOnly));
        before = await Totals();
        user.UserId = f.OwnerUserId;
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().ChangeAsync(f.HouseholdId, claim.Id, claim.Version, true, day, "MANUAL", ReimbursementStatus.Submitted, ct);
        var submitted = (await List(expense)).Single().Claim;
        Check(submitted.ExpectedAmount == 30 && submitted.SubmittedOn == day, "Manual submission does not reuse lifecycle");
        var submittedRow = (await MoneyBack("Submitted")).Items.Single(x => x.Id == claim.Id);
        Check(submittedRow.Amount == 30 && submittedRow.Outstanding == 30, "Submitted overview amounts are incorrect");
        Check(await Create(expense, payment.UpdatedAt, config) == claim.Id, "Retry after submission lost idempotency");
        Check(await Totals() == before, "Submission created actual income");
        Check(await OverviewStatus(expense) == "Submitted", "Submitted badge stale");
        var revised = RecurringReimbursementRule.Create("Edited provider", ReimbursementCalculation.Fixed, 50, null, f.HouseholdAccountId, 3, null);
        await Edit(claim.Id, submitted.Version, revised);
        var edited = (await List(expense)).Single().Claim;
        Check(edited.Provider == "Edited provider" && edited.PotentialAmount == 50 && edited.Status == ReimbursementStatus.Submitted
            && edited.SubmittedOn == day && edited.Reference == "MANUAL" && edited.ExpectedOn == day.AddDays(3) && edited.SubmissionReminderOn == null, "Editing lost submission history or timing");
        Check(edited.Events.Any(e => e.ChangeKind == "BeforeUpdate" && e.PotentialAmount == 30) && edited.Events.Any(e => e.ChangeKind == "Updated" && e.PotentialAmount == 50), "Edit audit snapshots missing");
        Check(edited.OriginalPaymentAmount == payment.Amount && await Totals() == before, "Edit changed source or actual money");
        await Throws<BusinessRuleException>(() => Edit(claim.Id, edited.Version, oversized));
        Check((await List(expense)).Single().Claim.Version == edited.Version && await Totals() == before, "Invalid edit changed claim or actual money");
        await Throws<ConflictException>(() => Edit(claim.Id, submitted.Version, config));
        user.UserId = f.MemberUserId;
        await Throws<NotFoundException>(() => Edit(claim.Id, edited.Version, config));
        user.UserId = f.OwnerUserId;
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().ChangeAsync(f.HouseholdId, claim.Id, edited.Version, false, null, null, ReimbursementStatus.Cancelled, ct);
        var cancelled = (await List(expense)).Single().Claim;
        await Throws<BusinessRuleException>(() => Edit(claim.Id, cancelled.Version, config));
        var retryExpense = await Post(Input());
        var retryPayment = await Payment(retryExpense);
        var retryId = await Create(retryExpense, retryPayment.UpdatedAt, viewOnly);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = await db.Accounts.Include(a => a.Owners).SingleAsync(a => a.Id == viewOnlyId, ct);
            account.Update(account.Name, account.Type, [f.MemberId], AccountVisibility.Household, true, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }
        Check(await Create(retryExpense, retryPayment.UpdatedAt, viewOnly) == retryId, "Identical retry failed after destination ownership changed");
        var newExpense = await Post(Input());
        var newPayment = await Payment(newExpense);
        await Throws<BusinessRuleException>(() => Create(newExpense, newPayment.UpdatedAt, viewOnly));
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = await db.Accounts.Include(a => a.Owners).SingleAsync(a => a.Id == viewOnlyId, ct);
            account.Update(account.Name, account.Type, [f.MemberId], AccountVisibility.Private, false, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }
        Check(await Create(retryExpense, retryPayment.UpdatedAt, viewOnly) == retryId, "Identical retry failed after destination visibility was lost");
        await Throws<NotFoundException>(() => Create(newExpense, newPayment.UpdatedAt, viewOnly));
        var recovery = (await RecoverableDestinations()).Single(r => r.ClaimId == retryId);
        user.UserId = f.MemberUserId;
        Check((await RecoverableDestinations()).All(r => r.ClaimId != retryId), "Destination recovery leaked another creator's claim");
        await Throws<NotFoundException>(() => Reassign(retryId, recovery.Version, f.HouseholdAccountId));
        user.UserId = f.OwnerUserId;
        await Throws<ConflictException>(() => Reassign(retryId, Guid.NewGuid(), f.HouseholdAccountId));
        await Throws<NotFoundException>(() => Reassign(retryId, recovery.Version, viewOnlyId));
        Guid differentCurrencyId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = Account.Create(f.HouseholdId, [], "Wrong currency recovery", AccountType.Cash,
                AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Huf, 0, day, clock.GetUtcNow());
            db.Accounts.Add(account); await db.SaveChangesAsync(ct); differentCurrencyId = account.Id;
        }
        await Throws<BusinessRuleException>(() => Reassign(retryId, recovery.Version, differentCurrencyId));
        var financesBeforeRecovery = await Totals();
        await Reassign(retryId, recovery.Version, f.HouseholdAccountId);
        var recovered = (await List(retryExpense)).Single();
        Check(recovered.Claim.DestinationAccountId == f.HouseholdAccountId && !recovered.VisibleAccountIds.Contains(viewOnlyId)
            && recovered.Claim.Events.Any(e => e.ChangeKind == "BeforeUpdate" && e.DestinationAccountId == viewOnlyId)
            && await Totals() == financesBeforeRecovery, "Destination recovery changed finances or lost its audit snapshot");
        Check((await RecoverableDestinations()).All(r => r.ClaimId != retryId), "Recovered destination still listed");
        Console.WriteLine("PASS manual reimbursement: atomic expense+claim, rollback, concurrent retries, no schedule, consent, snapshots, privacy and unchanged financial totals");

        async Task Edit(Guid id, Guid version, RecurringReimbursementRule configuration) { await using var scope = provider.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().UpdateAsync(f.HouseholdId, id, version, configuration, ct); }
        async Task<IReadOnlyList<ReimbursementDestinationRecovery>> RecoverableDestinations() { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().DestinationRecoveryAsync(f.HouseholdId, ct); }
        async Task Reassign(Guid id, Guid version, Guid destination) { await using var scope = provider.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().ReassignDestinationAsync(f.HouseholdId, id, version, destination, ct); }
        async Task<string?> DetailStatus(Guid id) { await using var scope = provider.CreateAsyncScope(); return (await scope.ServiceProvider.GetRequiredService<Averio.Application.Transactions.GetTransaction.GetTransactionHandler>().HandleAsync(f.HouseholdId, id, ct)).ReimbursementStatus; }
        async Task<string?> OverviewStatus(Guid id) { await using var scope = provider.CreateAsyncScope(); return (await scope.ServiceProvider.GetRequiredService<Averio.Application.Transactions.GetTransactions.GetTransactionsHandler>().HandleAsync(f.HouseholdId, null, null, null, null, null, 100, null, null, null, ct)).Items.Single(t => t.Id == id).ReimbursementStatus; }
        async Task<Guid> Post(CreateTransactionCommand command) { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(command, ct); }
        async Task<Guid> Create(Guid id, DateTimeOffset version, RecurringReimbursementRule configuration) { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().CreateAsync(f.HouseholdId, id, version, configuration, ct); }
        async Task<IReadOnlyList<ReimbursementResult>> List(Guid id) { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().ListAsync(f.HouseholdId, id, ct); }
        async Task<(IReadOnlyList<MoneyBackRow> Items, bool HasMore)> MoneyBack(string? status)
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<MoneyBackHandler>();
            var items = new List<MoneyBackRow>();
            (IReadOnlyList<MoneyBackRow> Items, bool HasMore) page;
            do { page = await handler.ListAsync(f.HouseholdId, items.Count, 100, status, ct); items.AddRange(page.Items); }
            while (page.HasMore);
            return (items, false);
        }
        async Task<Transaction> Payment(Guid id) { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Transactions.AsNoTracking().SingleAsync(t => t.Id == id); }
        async Task<(int, decimal, decimal)> Totals() { await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>(); return (await db.Transactions.CountAsync(), await db.Transactions.SumAsync(t => t.Amount), await db.TransactionAggregates.SumAsync(a => a.IncomeAmount)); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}

