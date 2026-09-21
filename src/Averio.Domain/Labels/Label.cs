namespace Averio.Domain.Labels;

public sealed class Label
{
    private Label() { }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid? AccountId { get; private set; }
    public LabelScope Scope { get; private set; }
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Label Create(
        Guid householdId,
        Guid? accountId,
        LabelScope scope,
        string name,
        DateTimeOffset now)
    {
        var normalizedName = Normalize(name);

        if (householdId == Guid.Empty)
            throw new ArgumentException("Household identifier is required.", nameof(householdId));
        if (!Enum.IsDefined(scope))
            throw new ArgumentException("Invalid label scope.", nameof(scope));
        if (normalizedName.Length is 0 or > 40)
            throw new ArgumentOutOfRangeException(nameof(name));
        if (scope == LabelScope.Household && accountId is not null)
            throw new ArgumentException("Household labels cannot reference an account.", nameof(accountId));
        if (scope == LabelScope.Account && (accountId is null || accountId == Guid.Empty))
            throw new ArgumentException("Account labels require an account.", nameof(accountId));

        return new Label
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            AccountId = accountId,
            Scope = scope,
            Name = name.Trim(),
            NormalizedName = normalizedName,
            CreatedAt = now,
        };
    }

    public static string Normalize(string value)
        => value.Trim().ToUpperInvariant();
}
