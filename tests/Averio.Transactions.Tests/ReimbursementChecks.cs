using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Averio.Domain.Common;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class ReimbursementChecks
{
    public static void Run()
    {
        var now = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        var day = new DateOnly(2026,11,1);
        var member = Guid.NewGuid();
        var rule = RecurringRule.Create(Guid.NewGuid(), member, Guid.NewGuid(), Guid.NewGuid(), "Mortgage", 150000,
            TransactionKind.Expense, TransactionVisibility.Household, RecurringFrequency.Monthly, day, null, false, now);
        var config = RecurringReimbursementRule.Create("Health fund", ReimbursementCalculation.Percentage, 20, 30000, rule.AccountId, 7, 2);
        rule.SetReimbursement(config);
        var payment = Transaction.Create(rule.HouseholdId, rule.AccountId, member, TransactionKind.Expense, TransactionVisibility.Household,
            CurrencyCode.Huf, null, null, day, [new("Mortgage", rule.CategoryId, "Housing", "home", 150000)], [], now);
        var claim = ReimbursementClaim.Create(rule, payment, now);
        Check(claim.PotentialAmount == 30000 && claim.ExpectedAmount == 0 && claim.ExpectedOn == null, "Unsubmitted opportunity is not expected income");
        Check(claim.SubmissionReminderOn == new DateOnly(2026,11,19), "Reminder anchored to recording, not scheduled date");
        rule.SetReimbursement(RecurringReimbursementRule.Create("New provider", ReimbursementCalculation.Fixed, 5, null, rule.AccountId, 30, null));
        Check(claim.PotentialAmount == 30000 && claim.Provider == "Health fund" && claim.PaymentDelayDays == 7, "Snapshot changed");
        Throws(() => claim.Submit(false, day, null, member, now));
        Throws(() => claim.Submit(true, day.AddDays(-1), null, member, now));
        Throws(() => claim.Submit(true, new(2026,11,18), null, member, now));
        Throws(() => claim.Submit(true, day, new string('x',161), member, now));
        claim.Submit(true, new(2026,11,3), "REF-1", member, now);
        Check(claim.ExpectedAmount == 30000 && claim.ExpectedOn == new DateOnly(2026,11,10) && claim.Events.Count == 2, "Submission estimate/history");
        Throws(() => claim.Submit(true, day, null, member, now));
        claim.Close(ReimbursementStatus.Rejected, member, now);
        Check(claim.ExpectedAmount == 0, "Rejected claim remains outstanding");
        claim.Submit(true, new(2026,11,16), "REF-2", member, now);
        Check(claim.Events.Any(e => e.Reference == "REF-1") && claim.Reference == "REF-2", "Resubmission lost history");
        claim.Close(ReimbursementStatus.Cancelled, member, now);
        Throws(() => claim.Submit(true, day, null, member, now));
        Check(payment.Amount == 150000 && payment.Kind == TransactionKind.Expense && payment.Status == TransactionStatus.Posted, "Claim mutated payment");
        Check(config.Estimate(10000) == 2000 && config.Estimate(200000) == 30000, "Percentage or cap");
        Check(RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Fixed, 20000, null, rule.AccountId, 0, null).Estimate(10000) == 10000, "Estimate exceeds source payment");
        var fullAmount = RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Fixed, payment.Amount, null, rule.AccountId, 0, null);
        var otherMember = Guid.NewGuid();
        Check(payment.CanUseForPrivateReimbursement(member) && !payment.CanUseForPrivateReimbursement(otherMember), "Source eligibility must follow its creator");
        Throws(() => ReimbursementClaim.CreateManual(payment, otherMember, fullAmount, new string('A', 64), now));
        var manual = ReimbursementClaim.CreateManual(payment, member, fullAmount, new string('A', 64), now);
        Check(manual.PotentialAmount == payment.Amount, "A full-expense reimbursement must be allowed");
        var tooMuch = RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Fixed, payment.Amount + 0.0001m, 1, rule.AccountId, 0, null);
        Throws(() => ReimbursementClaim.CreateManual(payment, member, tooMuch, new string('B', 64), now));
        var version = manual.Version;
        Throws(() => manual.Update(tooMuch, member, now));
        Check(manual.Version == version && manual.Events.Count == 1 && manual.PotentialAmount == payment.Amount,
            "Invalid estimate changed the claim or audit history");
        Throws(() => RecurringReimbursementRule.Create(" ", ReimbursementCalculation.Fixed, 1, null, rule.AccountId, 7, 2));
        Throws(() => RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Percentage, 101, null, rule.AccountId, 7, 2));
        Throws(() => RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Fixed, 0, null, rule.AccountId, 7, 2));
        Throws(() => RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Fixed, 1, null, rule.AccountId, 366, 2));
        Throws(() => RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Fixed, 1, null, rule.AccountId, 7, -1));
        var settled = ReimbursementClaim.CreateManual(payment, member, config, new string('C', 64), now);
        Transaction Credit(decimal amount) => Transaction.Create(payment.HouseholdId, payment.AccountId, member, TransactionKind.Income,
            TransactionVisibility.Household, payment.Currency, "Provider", "Original note", day,
            [new("Received", rule.CategoryId, "Income", "money", amount)], [], now);
        var first = Credit(10000); var second = Credit(20000);
        Throws(() => settled.LinkPayment(first, Guid.NewGuid(), new string('D',64), member, now));
        settled.Submit(true, day, null, member, now);
        var otherCredit = Transaction.Create(payment.HouseholdId, payment.AccountId, otherMember, TransactionKind.Income,
            TransactionVisibility.Household, payment.Currency, "Provider", null, day,
            [new("Received", rule.CategoryId, "Income", "money", 10000)], [], now);
        Throws(() => settled.LinkPayment(otherCredit, Guid.NewGuid(), new string('D',64), member, now));
        var match = settled.LinkPayment(first, Guid.NewGuid(), new string('D',64), member, now);
        Throws(() => settled.ReassignDestination(Guid.NewGuid(), member, now));
        Check(settled.Status == ReimbursementStatus.PartiallyReceived && settled.ReceivedAmount == 10000 && settled.OutstandingAmount == 20000, "Partial settlement");
        Throws(() => settled.Close(ReimbursementStatus.Cancelled, member, now));
        Throws(() => settled.LinkPayment(first, Guid.NewGuid(), new string('E',64), member, now));
        Throws(() => settled.LinkPayment(Credit(20000.0001m), Guid.NewGuid(), new string('E',64), member, now));
        Throws(() => settled.Update(RecurringReimbursementRule.Create("Fund", ReimbursementCalculation.Fixed, 9999, null, rule.AccountId, 0, null), member, now));
        settled.LinkPayment(second, Guid.NewGuid(), new string('E',64), member, now);
        Check(settled.Status == ReimbursementStatus.Received && settled.OutstandingAmount == 0, "Full settlement");
        settled.UnlinkPayment(match.Id, member, now);
        var unlinkedVersion = settled.Version;
        settled.UnlinkPayment(match.Id, member, now);
        Check(settled.Version == unlinkedVersion && settled.ReceivedAmount == 20000, "Repeated unlink mutated claim");
        Check(first.Kind == TransactionKind.Income && first.Amount == 10000 && first.Status == TransactionStatus.Posted && first.Note == "Original note", "Matching mutated bank movement");
        settled.LinkPayment(first, Guid.NewGuid(), new string('F',64), member, now);
        settled.UnlinkPayment(match.Id, member, now);
        Check(settled.Status == ReimbursementStatus.Received && settled.Payments.Count == 3, "Old unlink affected new match");
        var newDestination = Guid.NewGuid();
        Throws(() => manual.ReassignDestination(newDestination, Guid.NewGuid(), now));
        manual.ReassignDestination(newDestination, member, now);
        Check(manual.DestinationAccountId == newDestination && manual.Events.Count == 3 && manual.Events.Any(e => e.ChangeKind == "BeforeUpdate"),
            "Destination recovery lost audit history");
        Throws(() => claim.ReassignDestination(newDestination, member, now));
        using var db = new AverioDbContext(new DbContextOptionsBuilder<AverioDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var entity = db.Model.FindEntityType(typeof(ReimbursementClaim))!;
        Check(entity.GetIndexes().Any(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(ReimbursementClaim.TransactionId)), "Missing idempotency index");
        Check(entity.FindProperty(nameof(ReimbursementClaim.Version))!.IsConcurrencyToken, "Missing concurrency token");
        Check(Averio.Domain.Transactions.ReceivedCreditAllocationRule.Remaining(31m, [12m, 19m]) == 0m, "Pooled credit remaining amount");
        Throws(() => Averio.Domain.Transactions.ReceivedCreditAllocationRule.ValidateNew(31m, [12m], 19.0001m, 30m));
        Throws(() => Averio.Domain.Transactions.ReceivedCreditAllocationRule.ValidateNew(31m, [12m], 20m, 30m));
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected invalid reimbursement"); }
}


