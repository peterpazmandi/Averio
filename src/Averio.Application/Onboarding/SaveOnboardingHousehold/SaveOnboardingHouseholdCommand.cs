namespace Averio.Application.Onboarding.SaveOnboardingHousehold;

public sealed record SaveOnboardingHouseholdCommand(
    string HouseholdName,
    string CountryCode,
    string PreferredCurrency);