using System.Text.Json.Serialization;

namespace Averio.Contracts.Accounts;

public sealed record CreateAccountRequest(
    string Name,
    string Type,
    string Scope,
    IReadOnlyList<Guid> OwnerHouseholdMemberIds,
    string Visibility,
    bool IncludeInHouseholdReports,
    string Currency,
    [property: JsonRequired] string BalanceAnchorAmount,
    DateOnly BalanceAnchorDate);

public sealed record UpdateAccountRequest(
    string Name,
    string Type,
    IReadOnlyList<Guid> OwnerHouseholdMemberIds,
    string Visibility,
    bool IncludeInHouseholdReports,
    [property: JsonRequired] DateTimeOffset ExpectedUpdatedAt);

public sealed record CreateAccountResponse(Guid Id);

public sealed record AccountOwnerResponse(
    Guid HouseholdMemberId,
    string? DisplayName, bool IsFormerMember = false);

public sealed record AccountOwnerOptionsResponse(
    Guid CurrentMemberId,
    IReadOnlyList<AccountOwnerResponse> Members);

public sealed record AccountResponse(
    Guid Id,
    Guid HouseholdId,
    IReadOnlyList<AccountOwnerResponse> Owners,
    string Name,
    string Type,
    string Scope,
    string Visibility,
    bool IncludeInHouseholdReports,
    string Currency,
    string BalanceAnchorAmount,
    DateOnly BalanceAnchorDate,
    string CurrentBalanceAmount,
    string Status,
    DateTimeOffset UpdatedAt, DateTimeOffset? CutoverAtUtc = null);

