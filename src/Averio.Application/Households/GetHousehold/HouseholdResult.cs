namespace Averio.Application.Households.GetHousehold;

public sealed record HouseholdResult(
    Guid Id, string? Name, string? CountryCode, string? PreferredCurrency,
    bool OnboardingCompleted, string Role, Guid Version,
    IReadOnlyList<HouseholdMemberResult> Members);

public sealed record HouseholdMemberResult(
    Guid Id, string? DisplayName, string Role, DateTimeOffset JoinedAtUtc, bool IsCurrentUser);
