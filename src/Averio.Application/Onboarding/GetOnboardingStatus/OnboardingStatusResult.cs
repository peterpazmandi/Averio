namespace Averio.Application.Onboarding.GetOnboardingStatus;

public sealed record OnboardingStatusResult(
    bool Completed,
    string CurrentStep,
    string? DisplayName,
    Guid HouseholdId,
    string? HouseholdName,
    string? CountryCode,
    string? PreferredCurrency);