namespace Averio.Domain.Transactions;

public sealed class TransactionLabel
{
    private TransactionLabel() { }

    public Guid HouseholdId { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid LabelId { get; private set; }

    internal static TransactionLabel Create(
        Guid householdId,
        Guid transactionId,
        Guid labelId)
    {
        if (householdId == Guid.Empty)
            throw new ArgumentException("Household identifier is required.", nameof(householdId));
        if (transactionId == Guid.Empty)
            throw new ArgumentException("Transaction identifier is required.", nameof(transactionId));
        if (labelId == Guid.Empty)
            throw new ArgumentException("Label identifier is required.", nameof(labelId));

        return new TransactionLabel
        {
            HouseholdId = householdId,
            TransactionId = transactionId,
            LabelId = labelId,
        };
    }
}
