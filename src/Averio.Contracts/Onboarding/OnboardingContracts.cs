namespace Averio.Contracts.Onboarding;

public sealed record OnboardingStatusResponse(
    bool Completed,
    string? DisplayName,
    HouseholdSetupResponse Household);

public sealed record HouseholdSetupResponse(
    Guid HouseholdId,
    string? Name,
    string? CountryCode,
    string? PreferredCurrency);

public sealed record CompleteOnboardingRequest(
    string DisplayName,
    string HouseholdName,
    string CountryCode,
    string PreferredCurrency);