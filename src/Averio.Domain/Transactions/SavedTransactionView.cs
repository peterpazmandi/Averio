namespace Averio.Domain.Transactions;

public sealed class SavedTransactionView
{
    private SavedTransactionView() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid HouseholdMemberId { get; private set; }
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public string FiltersJson { get; private set; } = null!;
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static SavedTransactionView Create(Guid householdId, Guid memberId, string name, string filters, DateTimeOffset now)
    {
        var view = new SavedTransactionView { Id = Guid.NewGuid(), HouseholdId = householdId, HouseholdMemberId = memberId, CreatedAt = now };
        view.Update(name, filters, now);
        return view;
    }
    public void Update(string name, string filters, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80 || filters.Length > 4000) throw new ArgumentException("Invalid saved view.");
        Name = name.Trim(); NormalizedName = Name.ToUpperInvariant(); FiltersJson = filters;
        Version = Guid.NewGuid(); UpdatedAt = now;
    }
}
