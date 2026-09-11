namespace Averio.Contracts.Households;

public sealed record HouseholdResponse(
    Guid Id,
    string? Name,
    string? CountryCode,
    string? PreferredCurrency,
    bool OnboardingCompleted,
    string Role);

public sealed record UpdateHouseholdRequest(
    string? Name,
    string? CountryCode,
    string? PreferredCurrency);