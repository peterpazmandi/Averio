using Averio.Domain.Transactions;
namespace Averio.Domain.Recurring;

// A claim allocation, not a new bank movement. Retained after unlink for audit and retries.
public sealed class ReimbursementPayment
{
    private ReimbursementPayment() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid ClaimId { get; private set; }
    public Guid CreditTransactionId { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestFingerprint { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public Guid LinkedByMemberId { get; private set; }
    public DateTimeOffset LinkedAt { get; private set; }
    public Guid? UnlinkedByMemberId { get; private set; }
    public DateTimeOffset? UnlinkedAt { get; private set; }
    internal static ReimbursementPayment Create(ReimbursementClaim claim, Transaction credit, Guid operation, string fingerprint, Guid actor, DateTimeOffset now)
    {
        if (operation == Guid.Empty || fingerprint.Length != 64) throw new ArgumentException("Invalid operation.");
        return new() { Id = Guid.NewGuid(), HouseholdId = claim.HouseholdId, ClaimId = claim.Id,
            CreditTransactionId = credit.Id, OperationId = operation, RequestFingerprint = fingerprint,
            Amount = credit.Amount, ReceivedOn = credit.OccurredOn, LinkedByMemberId = actor, LinkedAt = now };
    }
    internal void Unlink(Guid actor, DateTimeOffset now) { UnlinkedAt = now; UnlinkedByMemberId = actor; }
}
