namespace Averio.Domain.Transactions;

public sealed record TransactionLabelDraft(
    string Name,
    TransactionLabelScope Scope);

public sealed class TransactionLabel
{
    private TransactionLabel() { }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid? AccountId { get; private set; }
    public TransactionLabelScope Scope { get; private set; }
    public string Name { get; private set; } = null!;

    internal static TransactionLabel Create(
        Guid householdId,
        Guid transactionId,
        Guid accountId,
        TransactionLabelDraft draft)
    {
        var name = draft.Name.Trim();

        if (name.Length is 0 or > 40)
            throw new ArgumentOutOfRangeException(nameof(draft.Name));
        if (!Enum.IsDefined(draft.Scope))
            throw new ArgumentException("Invalid label scope.", nameof(draft.Scope));

        return new TransactionLabel
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            TransactionId = transactionId,
            AccountId = draft.Scope == TransactionLabelScope.Account
                ? accountId
                : null,
            Scope = draft.Scope,
            Name = name,
        };
    }
}
