namespace Averio.Domain.Transactions;

public sealed record TransactionItemDraft(
    string Description,
    Guid CategoryId,
    string CategoryName,
    string CategoryIcon,
    decimal Amount, Guid? ExistingItemId = null);

public sealed class TransactionItem
{
    private TransactionItem() { }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid TransactionId { get; private set; }
    public int Position { get; private set; }
    public string Description { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public string CategoryName { get; private set; } = null!;
    public string CategoryIcon { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public bool HasRefundHistory { get; private set; }
    public decimal OriginalAmount => Amount + RefundedAmount;

    internal void ApplyRefund(decimal change)
    {
        if (change == 0 || decimal.Round(change, 4) != change || RefundedAmount + change < 0 || Amount - change < 0)
            throw new ArgumentException("Invalid item refund.");
        Amount -= change;
        RefundedAmount += change;
        HasRefundHistory = true;
    }

    internal void Retain(TransactionItemDraft draft, int position)
    {
        if (draft.ExistingItemId != Id)
            throw new ArgumentException("Cannot update another transaction item.");
        if (RefundedAmount > 0 && draft.Amount != OriginalAmount)
            throw new ArgumentException("Reverse item refunds before correcting the original paid amount.");
        var checkedItem = Create(HouseholdId, TransactionId, position, draft);
        Position = position;
        Description = checkedItem.Description;
        CategoryId = checkedItem.CategoryId;
        CategoryName = checkedItem.CategoryName;
        CategoryIcon = checkedItem.CategoryIcon;
        if (checkedItem.Amount < RefundedAmount) throw new ArgumentException("Item cost is below its refunds.");
        Amount = checkedItem.Amount - RefundedAmount;
    }

    internal static TransactionItem Create(
        Guid householdId,
        Guid transactionId,
        int position,
        TransactionItemDraft draft)
    {
        var description = draft.Description.Trim();
        var categoryName = draft.CategoryName.Trim();
        var categoryIcon = draft.CategoryIcon.Trim();

        if (description.Length is 0 or > 160)
            throw new ArgumentOutOfRangeException(nameof(draft.Description));
        if (draft.CategoryId == Guid.Empty)
            throw new ArgumentException("Category identifier is required.", nameof(draft.CategoryId));
        if (categoryName.Length is 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(draft.CategoryName));
        if (categoryIcon.Length is 0 or > 32)
            throw new ArgumentOutOfRangeException(nameof(draft.CategoryIcon));
        if (draft.Amount <= 0
            || draft.Amount > 999999999999999.9999m
            || decimal.Round(draft.Amount, 4) != draft.Amount)
        {
            throw new ArgumentOutOfRangeException(nameof(draft.Amount));
        }

        return new TransactionItem
        {
            Id = draft.ExistingItemId ?? Guid.NewGuid(),
            HouseholdId = householdId,
            TransactionId = transactionId,
            Position = position,
            Description = description,
            CategoryId = draft.CategoryId,
            CategoryName = categoryName,
            CategoryIcon = categoryIcon,
            Amount = draft.Amount,
        };
    }
}
