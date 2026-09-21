namespace Averio.Domain.Categories;

public sealed class Category
{
    private Category() { }

    public Guid Id { get; private set; }
    public Guid? HouseholdId { get; private set; }
    public Guid? ParentId { get; private set; }

    // System categories store only a stable localization key.
    // Custom categories store only the user supplied Name.
    public string? SystemKey { get; private set; }
    public string? Name { get; private set; }

    public CategoryKind Kind { get; private set; }
    public string Icon { get; private set; } = null!;
    public string AccentColor { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsBase { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsAvailableTo(Guid householdId)
        => IsBase || HouseholdId == householdId;

    public string NameReference
        => SystemKey
            ?? Name
            ?? throw new InvalidOperationException(
                "Category must have either a system key or a custom name.");

    public bool Supports(Averio.Domain.Transactions.TransactionKind kind)
        => Kind == CategoryKind.Both
            || (Kind == CategoryKind.Income
                && kind == Averio.Domain.Transactions.TransactionKind.Income)
            || (Kind == CategoryKind.Expense
                && kind == Averio.Domain.Transactions.TransactionKind.Expense);
}
