using Averio.Domain.Common;

namespace Averio.Domain.Households;

public sealed class Household
{
    private readonly List<HouseholdMember> _members = [];

    private Household()
    {
    }

    private Household(
        Guid id,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public void Depart(Guid userId, Guid? successorMemberId, DateTimeOffset now)
    {
        var leaving = _members.Single(x => x.UserId == userId && x.LeftAtUtc is null);
        var remaining = _members.Where(x => x.LeftAtUtc is null && x.Id != leaving.Id).ToArray();
        if (leaving.Role == HouseholdRole.Owner && remaining.Length > 0)
        {
            var successor = remaining.SingleOrDefault(x => x.Id == successorMemberId)
                ?? throw new InvalidOperationException("Choose a remaining member as owner.");
            _ = successor; // Promotion follows persistence of the departing owner, preserving the unique owner index.
        }
        leaving.Leave(now);
        if (remaining.Length == 0) ArchivedAtUtc = now;
        Version = Guid.NewGuid();
    }

    public Guid Version { get; private set; } = Guid.NewGuid();

    public string? Name { get; private set; }

    public string? CountryCode { get; private set; }

    public CurrencyCode? ReportingCurrency { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? OnboardingCompletedAtUtc { get; private set; }

    public bool IsOnboardingCompleted => OnboardingCompletedAtUtc.HasValue;

    public IReadOnlyCollection<HouseholdMember> Members => _members;
    public void AddMember(Guid userId, DateTimeOffset now)
    {
        if (_members.Any(x => x.UserId == userId && x.LeftAtUtc is null)) throw new InvalidOperationException("Already a member.");
        _members.Add(HouseholdMember.CreateMember(Id, userId, now));
        Version = Guid.NewGuid();
    }
    public void PromoteSuccessor(Guid memberId) => _members.Single(x => x.Id == memberId && x.LeftAtUtc is null).BecomeOwner();
    public DateTimeOffset? FinancialSetupCompletedAtUtc
    {
        get;
        private set;
    }

    public bool IsFinancialSetupCompleted =>
        IsOnboardingCompleted
        && FinancialSetupCompletedAtUtc.HasValue;

    public void CompleteFinancialSetup(DateTimeOffset now)
    {
        if (!IsOnboardingCompleted)
        {
            throw new InvalidOperationException("Household setup must be completed first.");
        }

        if (FinancialSetupCompletedAtUtc is null)
        {
            FinancialSetupCompletedAtUtc = now;
            Version = Guid.NewGuid();
        }
    }

    public static Household CreateForUser(
        Guid userId,
        DateTimeOffset now)
    {
        var household =
            new Household(
                Guid.NewGuid(),
                now);

        household._members.Add(
            HouseholdMember.CreateOwner(
                household.Id,
                userId,
                now));

        return household;
    }

    public void CompleteOnboarding(
        string name,
        string countryCode,
        CurrencyCode preferredCurrency,
        DateTimeOffset now)
    {
        UpdateProfile(
            name,
            countryCode,
            preferredCurrency);

        /*
         * Keep this idempotent.
         *
         * A retried PUT /onboarding shouldn't change
         * the original completion timestamp.
         */
        OnboardingCompletedAtUtc ??= now;
    }

    public void UpdateProfile(
        string name,
        string countryCode,
        CurrencyCode preferredCurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        var normalizedName = name.Trim();

        var normalizedCountryCode =
            countryCode
                .Trim()
                .ToUpperInvariant();

        if (normalizedName.Length is < 2 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(name));
        }

        if (normalizedCountryCode.Length != 2)
        {
            throw new ArgumentOutOfRangeException(nameof(countryCode));
        }

        Name = normalizedName;
        CountryCode = normalizedCountryCode;
        ReportingCurrency = preferredCurrency;
        Version = Guid.NewGuid();
    }
}

