using Averio.Domain.Common;

namespace Averio.Domain.Accounts;

public sealed class Account
{
    private readonly List<AccountOwner> _owners = [];

    private Account() { }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public IReadOnlyCollection<AccountOwner> Owners => _owners.AsReadOnly();
    public string Name { get; private set; } = null!;
    public AccountType Type { get; private set; }
    public AccountScope Scope { get; private set; }
    public AccountVisibility Visibility { get; private set; }
    public bool IncludeInHouseholdReports { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public decimal BalanceAnchorAmount { get; private set; }
    public DateOnly BalanceAnchorDate { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public DateTimeOffset? CutoverAtUtc { get; private set; }
    public void CloseForDeparture(DateTimeOffset now)
    {
        Archive(now);
        CutoverAtUtc = now;
        UpdatedAt = NextVersion(now);
    }

    public static Account Create(
        Guid householdId,
        IReadOnlyCollection<Guid> ownerHouseholdMemberIds,
        string name,
        AccountType type,
        AccountScope scope,
        AccountVisibility visibility,
        bool includeInHouseholdReports,
        CurrencyCode currency,
        decimal balanceAnchorAmount,
        DateOnly balanceAnchorDate,
        DateTimeOffset now)
    {
        if (householdId == Guid.Empty)
            throw new ArgumentException("Household identifier is required.", nameof(householdId));

        ArgumentNullException.ThrowIfNull(ownerHouseholdMemberIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Trim().Length > 100)
            throw new ArgumentOutOfRangeException(nameof(name));

        if (!Enum.IsDefined(type) || !Enum.IsDefined(scope) || !Enum.IsDefined(visibility))
            throw new ArgumentException("Invalid account configuration.");

        if (!CurrencyCode.IsSupported(currency.Value))
            throw new ArgumentException("Unsupported currency.", nameof(currency));

        if (balanceAnchorDate == default)
            throw new ArgumentException("Balance date is required.", nameof(balanceAnchorDate));

        const decimal maximumAmount = 999999999999999.9999m;
        if (balanceAnchorAmount < -maximumAmount || balanceAnchorAmount > maximumAmount
            || decimal.Round(balanceAnchorAmount, 4) != balanceAnchorAmount)
            throw new ArgumentOutOfRangeException(nameof(balanceAnchorAmount));

        var ownerIds = ownerHouseholdMemberIds.ToArray();
        if (ownerIds.Any(id => id == Guid.Empty) || ownerIds.Distinct().Count() != ownerIds.Length)
            throw new ArgumentException("Owner identifiers must be nonempty and unique.");

        if (scope == AccountScope.Personal && ownerIds.Length == 0)
            throw new InvalidOperationException("Personal accounts require at least one holder.");

        if (scope == AccountScope.Household
            && (visibility != AccountVisibility.Household || !includeInHouseholdReports))
            throw new InvalidOperationException("Household-scope accounts must be visible and included.");

        if (visibility == AccountVisibility.Private
            && (scope != AccountScope.Personal || includeInHouseholdReports))
            throw new InvalidOperationException("Private accounts must be personal and excluded.");

        var account = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            Name = name.Trim(),
            Type = type,
            Scope = scope,
            Visibility = visibility,
            IncludeInHouseholdReports = includeInHouseholdReports,
            Currency = currency,
            BalanceAnchorAmount = balanceAnchorAmount,
            BalanceAnchorDate = balanceAnchorDate,
            Status = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var ownerId in ownerIds)
            account._owners.Add(new AccountOwner(householdId, account.Id, ownerId));

        return account;
    }

    public void Update(
        string name,
        AccountType type,
        IReadOnlyCollection<Guid> ownerHouseholdMemberIds,
        AccountVisibility visibility,
        bool includeInHouseholdReports,
        DateTimeOffset now)
    {
        if (Status != AccountStatus.Active)
            throw new InvalidOperationException("Restore the account before editing it.");

        // Reuse creation invariants without changing identity, scope, currency or anchor.
        var validated = Create(HouseholdId, ownerHouseholdMemberIds, name, type, Scope,
            visibility, includeInHouseholdReports, Currency, BalanceAnchorAmount,
            BalanceAnchorDate, now);

        Name = validated.Name;
        Type = validated.Type;
        Visibility = validated.Visibility;
        IncludeInHouseholdReports = validated.IncludeInHouseholdReports;
        _owners.RemoveAll(owner => !ownerHouseholdMemberIds.Contains(owner.HouseholdMemberId));
        foreach (var memberId in ownerHouseholdMemberIds)
            if (!_owners.Any(owner => owner.HouseholdMemberId == memberId))
                _owners.Add(new AccountOwner(HouseholdId, Id, memberId));
        UpdatedAt = NextVersion(now);
    }

    public void Archive(DateTimeOffset now)
    {
        if (Status == AccountStatus.Archived) return;
        Status = AccountStatus.Archived;
        ArchivedAt = now;
        UpdatedAt = NextVersion(now);
    }

    public void Restore(DateTimeOffset now)
    {
        if (CutoverAtUtc is not null) throw new InvalidOperationException("Historical accounts cannot be restored.");
        if (Status == AccountStatus.Active) return;
        Status = AccountStatus.Active;
        ArchivedAt = null;
        UpdatedAt = NextVersion(now);
    }

    // PostgreSQL timestamps have microsecond precision. Always advance, even under a fixed clock.
    private DateTimeOffset NextVersion(DateTimeOffset now) => new(
        Math.Max(now.UtcTicks - now.UtcTicks % 10, UpdatedAt.UtcTicks - UpdatedAt.UtcTicks % 10 + 10),
        TimeSpan.Zero);
}

