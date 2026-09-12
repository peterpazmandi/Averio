using Averio.Application.Accounts.Common;

namespace Averio.Application.Dashboard.GetDashboard;

public sealed record DashboardResult(
    string? DisplayName,
    string ReportingCurrency,
    IReadOnlyList<HouseholdBalanceResult> HouseholdBalances,
    IReadOnlyList<AccountResult> Accounts);

public sealed record HouseholdBalanceResult(
    string Currency,
    decimal Amount,
    DateOnly EarliestBalanceDate,
    DateOnly LatestBalanceDate);