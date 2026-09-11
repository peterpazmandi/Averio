using Averio.Domain.Common;

namespace Averio.Domain.Accounts;

public sealed class Account
{
    private Account()
    {
    }

    private Account(
        Guid id,
        Guid householdId,
        Guid? ownerHouseholdMemberId,
        string name,
        AccountType type,
        AccountOwnership ownership,
        AccountVisibility visibility,
        bool includeInHouseholdReports,
        CurrencyCode currency,
        decimal balanceAnchorAmount,
        DateOnly balanceAnchorDate,
        DateTimeOffset createdAt)
    {
        Id = id;
        HouseholdId = householdId;
        OwnerHouseholdMemberId = ownerHouseholdMemberId;

        Name = name;
        Type = type;
        Ownership = ownership;
        Visibility = visibility;
        IncludeInHouseholdReports = includeInHouseholdReports;

        Currency = currency;

        BalanceAnchorAmount = balanceAnchorAmount;
        BalanceAnchorDate = balanceAnchorDate;

        Status = AccountStatus.Active;

        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid HouseholdId { get; private set; }

    public Guid? OwnerHouseholdMemberId { get; private set; }

    public string Name { get; private set; } = null!;

    public AccountType Type { get; private set; }

    public AccountOwnership Ownership { get; private set; }

    public AccountVisibility Visibility { get; private set; }

    public bool IncludeInHouseholdReports { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public decimal BalanceAnchorAmount { get; private set; }

    public DateOnly BalanceAnchorDate { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public static Account Create(
        Guid householdId,
        Guid currentMemberId,
        string name,
        AccountType type,
        AccountOwnership ownership,
        AccountVisibility visibility,
        bool includeInHouseholdReports,
        CurrencyCode currency,
        decimal balanceAnchorAmount,
        DateOnly balanceAnchorDate,
        DateTimeOffset now)
    {
        ValidateConfiguration(
            ownership,
            visibility,
            includeInHouseholdReports);

        Guid? ownerMemberId = ownership == AccountOwnership.Personal
                ? currentMemberId
                : null;

        return new Account(
            Guid.NewGuid(),
            householdId,
            ownerMemberId,
            name.Trim(),
            type,
            ownership,
            visibility,
            includeInHouseholdReports,
            currency,
            balanceAnchorAmount,
            balanceAnchorDate,
            now);
    }
    public void Archive(DateTimeOffset now)
    {
        if (Status == AccountStatus.Archived)
        {
            return;
        }

        Status = AccountStatus.Archived;
        ArchivedAt = now;
        UpdatedAt = now;
    }

    public void Restore(DateTimeOffset now)
    {
        if (Status == AccountStatus.Active)
        {
            return;
        }

        Status = AccountStatus.Active;
        ArchivedAt = null;
        UpdatedAt = now;
    }

    private static void ValidateConfiguration(
        AccountOwnership ownership,
        AccountVisibility visibility,
        bool includeInHouseholdReports)
    {
        if (ownership == AccountOwnership.Household &&
            visibility != AccountVisibility.Household)
        {
            throw new InvalidOperationException(
                "A household account cannot be private.");
        }

        if (ownership == AccountOwnership.Household &&
            !includeInHouseholdReports)
        {
            throw new InvalidOperationException(
                "A household-owned account must participate in household reporting.");
        }

        if (visibility == AccountVisibility.Private &&
            ownership != AccountOwnership.Personal)
        {
            throw new InvalidOperationException(
                "Only personal accounts can be private.");
        }

        if (visibility == AccountVisibility.Private &&
            includeInHouseholdReports)
        {
            throw new InvalidOperationException(
                "Private accounts cannot participate in household reporting.");
        }
    }
}