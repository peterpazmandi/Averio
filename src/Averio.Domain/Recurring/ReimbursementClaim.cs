using Averio.Domain.Transactions;
namespace Averio.Domain.Recurring;

public enum ReimbursementStatus { NeedsSubmission, Submitted, Rejected, Cancelled }

public sealed class ReimbursementClaim
{
    private ReimbursementClaim() { }
    private readonly List<ReimbursementClaimEvent> _events = [];
    public IReadOnlyCollection<ReimbursementClaimEvent> Events => _events.AsReadOnly();
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid HouseholdMemberId { get; private set; }
    public Guid? RuleId { get; private set; }
    public string? CreationFingerprint { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string Currency { get; private set; } = null!;
    public decimal PotentialAmount { get; private set; }
    public decimal OriginalPaymentAmount { get; private set; }
    public DateOnly PaymentOn { get; private set; }
    public int PaymentDelayDays { get; private set; }
    public DateOnly? SubmissionReminderOn { get; private set; }
    public DateOnly? SubmittedOn { get; private set; }
    public DateOnly? ExpectedOn { get; private set; }
    public string? Reference { get; private set; }
    public ReimbursementStatus Status { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public decimal ExpectedAmount => Status == ReimbursementStatus.Submitted ? PotentialAmount : 0m;

    public static ReimbursementClaim Create(RecurringRule rule, Transaction payment, DateTimeOffset now)
    {
        var config = rule.Reimbursement ?? throw new ArgumentException("Reimbursement is not enabled.");
        if (rule.Kind != TransactionKind.Expense || payment.Kind != TransactionKind.Expense || payment.Status != TransactionStatus.Posted
            || payment.HouseholdId != rule.HouseholdId || payment.AccountId != rule.AccountId)
            throw new ArgumentException("A recorded expense is required.");
        return Create(payment, rule.HouseholdMemberId, config, now, rule.Id);
    }
    public static ReimbursementClaim CreateManual(Transaction payment, Guid memberId, RecurringReimbursementRule config,
        string fingerprint, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(fingerprint) || fingerprint.Length != 64) throw new ArgumentException("Invalid fingerprint.");
        var claim = Create(payment, memberId, config, now, null);
        claim.CreationFingerprint = fingerprint;
        return claim;
    }
    private static ReimbursementClaim Create(Transaction payment, Guid memberId, RecurringReimbursementRule config, DateTimeOffset now, Guid? ruleId)
    {
        if (memberId == Guid.Empty || payment.Kind != TransactionKind.Expense || payment.Status != TransactionStatus.Posted || payment.IsArchived)
            throw new ArgumentException("A recorded expense is required.");
        var estimate = config.Estimate(payment.Amount);
        if (estimate <= 0) throw new ArgumentException("Estimate rounds to zero.");
        var claim = new ReimbursementClaim { Id = Guid.NewGuid(), HouseholdId = payment.HouseholdId,
            HouseholdMemberId = memberId, RuleId = ruleId, TransactionId = payment.Id,
            DestinationAccountId = config.DestinationAccountId, Provider = config.Provider, Currency = payment.Currency.Value,
            PotentialAmount = estimate, OriginalPaymentAmount = payment.Amount, PaymentOn = payment.OccurredOn,
            PaymentDelayDays = config.PaymentDelayDays, CreatedAt = now,
            SubmissionReminderOn = config.SubmissionReminderDays is int days ? DateOnly.FromDateTime(now.UtcDateTime).AddDays(days) : null };
        claim.Record(memberId, now, "Created");
        return claim;
    }
    public void Update(RecurringReimbursementRule configuration, Guid actor, DateTimeOffset now)
    {
        if (Status == ReimbursementStatus.Cancelled || actor == Guid.Empty) throw new ArgumentException("Claim is closed.");
        var amount = configuration.Estimate(OriginalPaymentAmount);
        if (amount <= 0 || (SubmittedOn is DateOnly submitted && submitted.DayNumber > DateOnly.MaxValue.DayNumber - configuration.PaymentDelayDays))
            throw new ArgumentException("Invalid claim estimate.");
        var reminder = configuration.SubmissionReminderDays is int days ? DateOnly.FromDateTime(CreatedAt.UtcDateTime).AddDays(days) : (DateOnly?)null;
        // Preserve pre-edit details even for claims created before snapshot events existed.
        Record(actor, now, "BeforeUpdate");
        Provider = configuration.Provider; PotentialAmount = amount; DestinationAccountId = configuration.DestinationAccountId;
        PaymentDelayDays = configuration.PaymentDelayDays; SubmissionReminderOn = reminder;
        ExpectedOn = SubmittedOn?.AddDays(PaymentDelayDays);
        Record(actor, now, "Updated");
    }
    public void Submit(bool confirmed, DateOnly submittedOn, string? reference, Guid actor, DateTimeOffset now)
    {
        if (!confirmed || Status is not (ReimbursementStatus.NeedsSubmission or ReimbursementStatus.Rejected)
            || submittedOn < PaymentOn || submittedOn > DateOnly.FromDateTime(now.UtcDateTime)
            || reference?.Trim().Length > 160 || submittedOn.DayNumber > DateOnly.MaxValue.DayNumber - PaymentDelayDays)
            throw new ArgumentException("Invalid receipt submission.");
        SubmittedOn = submittedOn; ExpectedOn = submittedOn.AddDays(PaymentDelayDays);
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        Status = ReimbursementStatus.Submitted; Record(actor, now);
    }
    public void Close(ReimbursementStatus status, Guid actor, DateTimeOffset now)
    {
        if (status is not (ReimbursementStatus.Cancelled or ReimbursementStatus.Rejected)
            || Status == ReimbursementStatus.Cancelled || (status == ReimbursementStatus.Rejected && Status != ReimbursementStatus.Submitted))
            throw new ArgumentException("Invalid claim transition.");
        Status = status; Record(actor, now);
    }
    private void Record(Guid actor, DateTimeOffset now, string changeKind = "StatusChanged")
    {
        if (actor == Guid.Empty) throw new ArgumentException("Actor required.");
        Version = Guid.NewGuid(); UpdatedAt = now;
        _events.Add(ReimbursementClaimEvent.Create(this, actor, now, changeKind, _events.Count));
    }
}

public sealed class ReimbursementClaimEvent
{
    private ReimbursementClaimEvent() { }
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid ActorMemberId { get; private set; }
    public ReimbursementStatus Status { get; private set; }
    public DateOnly? SubmittedOn { get; private set; }
    public string? Reference { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public int? Sequence { get; private set; }
    public string? ChangeKind { get; private set; }
    public string? Provider { get; private set; }
    public decimal? PotentialAmount { get; private set; }
    public Guid? DestinationAccountId { get; private set; }
    public int? PaymentDelayDays { get; private set; }
    public DateOnly? SubmissionReminderOn { get; private set; }
    public DateOnly? ExpectedOn { get; private set; }
    internal static ReimbursementClaimEvent Create(ReimbursementClaim claim, Guid actor, DateTimeOffset now, string changeKind, int sequence) => new() {
        Id = Guid.NewGuid(), ClaimId = claim.Id, HouseholdId = claim.HouseholdId, ActorMemberId = actor,
        Status = claim.Status, SubmittedOn = claim.SubmittedOn, Reference = claim.Reference, RecordedAt = now,
        Sequence = sequence, ChangeKind = changeKind, Provider = claim.Provider, PotentialAmount = claim.PotentialAmount, DestinationAccountId = claim.DestinationAccountId,
        PaymentDelayDays = claim.PaymentDelayDays, SubmissionReminderOn = claim.SubmissionReminderOn, ExpectedOn = claim.ExpectedOn };
}
