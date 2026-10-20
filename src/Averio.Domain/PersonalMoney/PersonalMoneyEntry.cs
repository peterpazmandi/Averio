namespace Averio.Domain.PersonalMoney;

public enum PersonalMoneyEntryKind
{
    Grant = 1,
    Funding = 2,
    PrivateCharge = 3,
    SharedCharge = 4,
    PrivateRefund = 5,
    SharedRefund = 6,
    Surrender = 7
}

// Signed immutable events. Funding is explicitly excluded from entitlement.
// Corrections create new entries; no bank account balance is persisted here.
public sealed class PersonalMoneyEntry
{
    private PersonalMoneyEntry() { }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid WalletId { get; private set; }
    public PersonalMoneyEntryKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public Guid? TransferId { get; private set; }
    public Guid? TransactionId { get; private set; }
    public Guid? RelatedTransactionId { get; private set; }
    public Guid? ReversesEntryId { get; private set; }
    public Guid? OperationId { get; private set; }
    public decimal? CorrectionExpectedAmount { get; private set; }
    public int? CorrectionExpectedVersion { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public static PersonalMoneyEntry Create(Guid householdId, Guid walletId,
        PersonalMoneyEntryKind kind, decimal signedAmount, DateOnly occurredOn,
        DateTimeOffset now, Guid? transferId = null, Guid? transactionId = null,
        Guid? reversesEntryId = null, Guid? operationId = null,
        decimal? correctionExpectedAmount = null, int? correctionExpectedVersion = null,
        Guid? relatedTransactionId = null)
    {
        if (householdId == Guid.Empty || walletId == Guid.Empty || !Enum.IsDefined(kind)
            || occurredOn == default || signedAmount == 0
            || decimal.Abs(signedAmount) > 999999999999999.9999m
            || decimal.Round(signedAmount, 4) != signedAmount)
            throw new ArgumentException("Invalid personal-money ledger entry.");
        if (kind == PersonalMoneyEntryKind.Funding && transferId is null
            || kind is PersonalMoneyEntryKind.PrivateCharge or PersonalMoneyEntryKind.SharedCharge
                or PersonalMoneyEntryKind.PrivateRefund or PersonalMoneyEntryKind.SharedRefund && transactionId is null
            || kind is PersonalMoneyEntryKind.PrivateRefund or PersonalMoneyEntryKind.SharedRefund && relatedTransactionId is null
            || relatedTransactionId is not null && (transactionId is null || relatedTransactionId == transactionId)
            || transferId is not null && transactionId is not null)
            throw new ArgumentException("Invalid ledger source.");
        return new PersonalMoneyEntry
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, WalletId = walletId,
            Kind = kind, Amount = signedAmount, OccurredOn = occurredOn,
            RecordedAt = now, TransferId = transferId, TransactionId = transactionId,
            RelatedTransactionId = relatedTransactionId,
            ReversesEntryId = reversesEntryId, OperationId = operationId,
            CorrectionExpectedAmount = correctionExpectedAmount, CorrectionExpectedVersion = correctionExpectedVersion
        };
    }
}
