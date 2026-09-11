namespace Averio.Contracts.Accounts;

public sealed record CreateAccountRequest(
    string Name,
    string Type,
    string Ownership,
    string Visibility,
    bool IncludeInHouseholdReports,
    string Currency,
    decimal BalanceAnchorAmount,
    DateOnly BalanceAnchorDate);

public sealed record CreateAccountResponse(Guid Id);

public sealed record AccountResponse(
    Guid Id,
    Guid HouseholdId,
    Guid? OwnerHouseholdMemberId,
    string Name,
    string Type,
    string Ownership,
    string Visibility,
    bool IncludeInHouseholdReports,
    string Currency,
    decimal BalanceAnchorAmount,
    DateOnly BalanceAnchorDate,
    string Status);