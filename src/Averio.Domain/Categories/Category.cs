using System.Text.RegularExpressions;

namespace Averio.Domain.Categories;

public sealed class Category
{
    private Category() { }

    public Guid Id { get; private set; }
    public Guid? HouseholdId { get; private set; }
    public Guid? ParentId { get; private set; }
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

    public bool IsAvailableTo(Guid householdId) => IsBase || HouseholdId == householdId;
    public string NameReference => SystemKey ?? Name
        ?? throw new InvalidOperationException("A category needs a name source.");

    public bool Supports(Averio.Domain.Transactions.TransactionKind kind)
        => Kind == CategoryKind.Both
            || (Kind == CategoryKind.Income && kind == Averio.Domain.Transactions.TransactionKind.Income)
            || (Kind == CategoryKind.Expense && kind == Averio.Domain.Transactions.TransactionKind.Expense);

    public static Category Create(Guid householdId, string name, CategoryKind kind,
        string icon, string accentColor, Category? parent, DateTimeOffset now)
    {
        if (householdId == Guid.Empty) throw new ArgumentException("A household is required.");
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var category = new Category
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, Kind = kind,
            CreatedAt = Truncate(now), UpdatedAt = Truncate(now)
        };
        category.SetDetails(name, icon, accentColor, parent);
        return category;
    }

    public void Update(string name, string icon, string accentColor, Category? parent, DateTimeOffset now)
    {
        EnsureCustom();
        if (IsArchived) throw new InvalidOperationException("Restore the category before editing.");
        SetDetails(name, icon, accentColor, parent);
        AdvanceVersion(now);
    }

    public void SetArchived(bool archived, DateTimeOffset now)
    {
        EnsureCustom();
        if (IsArchived == archived) return;
        IsArchived = archived;
        AdvanceVersion(now);
    }

    public bool CanHaveParent(Category parent)
        => parent.Id != Id && !parent.IsArchived
            && parent.IsAvailableTo(HouseholdId!.Value)
            && (parent.Kind == CategoryKind.Both || parent.Kind == Kind);

    private void SetDetails(string name, string icon, string accentColor, Category? parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        if (name.Trim().Length > 100 || icon.Trim().Length > 32)
            throw new ArgumentOutOfRangeException(nameof(name));
        if (accentColor is null || !Regex.IsMatch(accentColor, "^#[0-9A-Fa-f]{6}$"))
            throw new ArgumentException("Use a six-digit hex color.");
        if (parent is not null && !CanHaveParent(parent))
            throw new InvalidOperationException("Invalid category parent.");
        Name = name.Trim();
        Icon = icon.Trim();
        AccentColor = accentColor.ToUpperInvariant();
        ParentId = parent?.Id;
    }

    private void EnsureCustom()
    {
        if (IsBase) throw new InvalidOperationException("System categories are read-only.");
    }

    private static DateTimeOffset Truncate(DateTimeOffset now)
        => new(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);

    private void AdvanceVersion(DateTimeOffset now)
        => UpdatedAt = new(Math.Max(Truncate(now).UtcTicks, UpdatedAt.UtcTicks + 10), TimeSpan.Zero);
}
