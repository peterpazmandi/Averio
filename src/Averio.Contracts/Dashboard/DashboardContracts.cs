using Averio.Contracts.Accounts;

namespace Averio.Contracts.Dashboard;

public sealed record DashboardResponse(
    string? DisplayName,
    string ReportingCurrency,
    IReadOnlyList<HouseholdBalanceResponse> HouseholdBalances,
    IReadOnlyList<AccountResponse> Accounts);

public sealed record HouseholdBalanceResponse(
    string Currency,
    string Amount,
    DateOnly EarliestBalanceDate,
    DateOnly LatestBalanceDate);
