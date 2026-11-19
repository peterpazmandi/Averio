namespace Averio.Domain.Transactions;

// One actual returned-money record belongs to exactly one original receipt item.
public sealed class ItemRefund
{
    private ItemRefund() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid TransactionItemId { get; private set; }
    public Guid CreditTransactionId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid CreatedByMemberId { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledByMemberId { get; private set; }
    public Guid Version { get; private set; }
    public string RequestFingerprint { get; private set; } = null!;
    public static ItemRefund Create(Transaction purchase, Guid itemId, Transaction credit, Guid operationId, Guid actor, DateTimeOffset now, string fingerprint)
    {
        var item = purchase.Items.SingleOrDefault(x => x.Id == itemId) ?? throw new ArgumentException("Unknown item.");
        if (purchase.Kind != TransactionKind.Expense || purchase.Status != TransactionStatus.Posted
            || credit.Kind != TransactionKind.Refund || credit.Status != TransactionStatus.Posted
            || purchase.Id == credit.Id || purchase.HouseholdId != credit.HouseholdId || purchase.AccountId != credit.AccountId
            || purchase.Currency != credit.Currency || purchase.Visibility != credit.Visibility
            || credit.OccurredOn < purchase.OccurredOn || credit.OccurredOn > DateOnly.FromDateTime(now.UtcDateTime)
            || credit.Amount > item.Amount || operationId == Guid.Empty || actor == Guid.Empty || fingerprint.Length != 64)
            throw new ArgumentException("Invalid item refund.");
        return new() { Id = Guid.NewGuid(), HouseholdId = purchase.HouseholdId, TransactionId = purchase.Id,
            TransactionItemId = itemId, CreditTransactionId = credit.Id, OperationId = operationId, CreatedByMemberId = actor,
            Amount = credit.Amount, ReceivedOn = credit.OccurredOn, CreatedAt = now, Version = Guid.NewGuid(), RequestFingerprint = fingerprint };
    }
    public void Cancel(Guid actor, DateTimeOffset now)
    {
        if (CancelledAt is not null || actor == Guid.Empty) throw new ArgumentException("Refund is already reversed.");
        CancelledAt = now; CancelledByMemberId = actor; Version = Guid.NewGuid();
    }
}
