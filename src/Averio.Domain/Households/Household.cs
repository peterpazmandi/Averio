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

    public Guid Version { get; private set; } = Guid.NewGuid();

    public string? Name { get; private set; }

    public string? CountryCode { get; private set; }

    public CurrencyCode? ReportingCurrency { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? OnboardingCompletedAtUtc { get; private set; }

    public bool IsOnboardingCompleted => OnboardingCompletedAtUtc.HasValue;

    public IReadOnlyCollection<HouseholdMember> Members => _members;
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
