namespace Averio.Contracts.Households;

public sealed record HouseholdResponse(
    Guid Id, string? Name, string? CountryCode, string? PreferredCurrency,
    bool OnboardingCompleted, string Role, Guid Version,
    IReadOnlyList<HouseholdMemberResponse> Members);

public sealed record HouseholdMemberResponse(
    Guid Id, string? DisplayName, string Role, DateTimeOffset JoinedAtUtc, bool IsCurrentUser);

public sealed record UpdateHouseholdRequest(
    string? Name, string? CountryCode, string? PreferredCurrency, Guid ExpectedVersion);
