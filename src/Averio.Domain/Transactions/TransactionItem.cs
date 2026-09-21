namespace Averio.Domain.Transactions;

public sealed record TransactionItemDraft(
    string Description,
    string? CategoryName,
    decimal Amount);

public sealed class TransactionItem
{
    private TransactionItem() { }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid TransactionId { get; private set; }
    public int Position { get; private set; }
    public string Description { get; private set; } = null!;
    public string? CategoryName { get; private set; }
    public decimal Amount { get; private set; }

    internal static TransactionItem Create(
        Guid householdId,
        Guid transactionId,
        int position,
        TransactionItemDraft draft)
    {
        var description = draft.Description.Trim();
        var categoryName = Normalize(draft.CategoryName);

        if (description.Length is 0 or > 160)
            throw new ArgumentOutOfRangeException(nameof(draft.Description));
        if (categoryName?.Length > 80)
            throw new ArgumentOutOfRangeException(nameof(draft.CategoryName));
        if (draft.Amount <= 0
            || draft.Amount > 999999999999999.9999m
            || decimal.Round(draft.Amount, 4) != draft.Amount)
        {
            throw new ArgumentOutOfRangeException(nameof(draft.Amount));
        }

        return new TransactionItem
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            TransactionId = transactionId,
            Position = position,
            Description = description,
            CategoryName = categoryName,
            Amount = draft.Amount,
        };
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
