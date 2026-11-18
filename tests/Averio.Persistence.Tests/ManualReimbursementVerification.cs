using Averio.Application.Common.Exceptions;
using Averio.Application.Recurring;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.Common;
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
        var before = await Totals();
        await Throws<BusinessRuleException>(() => Post(Input(config, TransactionKind.Income)));
        Check(await Totals() == before, "Invalid income claim left a transaction behind");
        var tiny = RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Percentage, 0.0001m, null, f.HouseholdAccountId, 0, null);
        var tinyInput = Input(tiny) with { Items = [new TransactionItemInput("Small", TestIds.Groceries, 0.0001m)] };
        await Throws<BusinessRuleException>(() => Post(tinyInput));
        Check(await Totals() == before, "Zero estimate did not roll back");
        var expense = await Post(Input());
        var payment = await Payment(expense);
        before = await Totals();
        await Throws<ConflictException>(() => Create(expense, payment.UpdatedAt.AddSeconds(-1), config));
        var hidden = RecurringReimbursementRule.Create("Private destination", ReimbursementCalculation.Fixed, 30, null, f.PrivateAccountId, 7, 0);
        user.UserId = f.MemberUserId;
        await Throws<NotFoundException>(() => Create(expense, payment.UpdatedAt, hidden));
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
        Check(await DetailStatus(expense) == null && await OverviewStatus(expense) == null, "Shared expense badge leaked private claim");
        await Throws<ConflictException>(() => Create(expense, payment.UpdatedAt, config));
        await Throws<NotFoundException>(() => Create(Guid.NewGuid(), payment.UpdatedAt, config));
        user.UserId = f.OwnerUserId;
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().ChangeAsync(f.HouseholdId, claim.Id, claim.Version, true, day, "MANUAL", ReimbursementStatus.Submitted, ct);
        var submitted = (await List(expense)).Single().Claim;
        Check(submitted.ExpectedAmount == 30 && submitted.SubmittedOn == day, "Manual submission does not reuse lifecycle");
        Check(await Create(expense, payment.UpdatedAt, config) == claim.Id, "Retry after submission lost idempotency");
        Check(await Totals() == before, "Submission created actual income");
        Check(await OverviewStatus(expense) == "Submitted", "Submitted badge stale");
        Console.WriteLine("PASS manual reimbursement: atomic expense+claim, rollback, concurrent retries, no schedule, consent, snapshots, privacy and unchanged financial totals");

        async Task<string?> DetailStatus(Guid id) { await using var scope = provider.CreateAsyncScope(); return (await scope.ServiceProvider.GetRequiredService<Averio.Application.Transactions.GetTransaction.GetTransactionHandler>().HandleAsync(f.HouseholdId, id, ct)).ReimbursementStatus; }
        async Task<string?> OverviewStatus(Guid id) { await using var scope = provider.CreateAsyncScope(); return (await scope.ServiceProvider.GetRequiredService<Averio.Application.Transactions.GetTransactions.GetTransactionsHandler>().HandleAsync(f.HouseholdId, null, null, null, null, null, 100, null, null, null, ct)).Items.Single(t => t.Id == id).ReimbursementStatus; }
        async Task<Guid> Post(CreateTransactionCommand command) { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(command, ct); }
        async Task<Guid> Create(Guid id, DateTimeOffset version, RecurringReimbursementRule configuration) { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().CreateAsync(f.HouseholdId, id, version, configuration, ct); }
        async Task<IReadOnlyList<ReimbursementResult>> List(Guid id) { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ReimbursementHandler>().ListAsync(f.HouseholdId, id, ct); }
        async Task<Transaction> Payment(Guid id) { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Transactions.AsNoTracking().SingleAsync(t => t.Id == id); }
        async Task<(int, decimal, decimal)> Totals() { await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>(); return (await db.Transactions.CountAsync(), await db.Transactions.SumAsync(t => t.Amount), await db.TransactionAggregates.SumAsync(a => a.IncomeAmount)); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}

