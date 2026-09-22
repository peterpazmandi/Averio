using Averio.Application.Accounts.Common;
using Averio.Application.Transactions.GetTransactions;

namespace Averio.Application.Dashboard.GetDashboard;

public sealed record DashboardResult(
    string? DisplayName,
    string ReportingCurrency,
    IReadOnlyList<HouseholdBalanceResult> HouseholdBalances,
    IReadOnlyList<AccountResult> Accounts,
    IReadOnlyList<DashboardCashFlowPeriodResult> CashFlow,
    IReadOnlyList<TransactionResult> LatestTransactions);

public sealed record HouseholdBalanceResult(
    string Currency,
    decimal Amount,
    DateOnly EarliestBalanceDate,
    DateOnly LatestBalanceDate);

public sealed record DashboardCashFlowPeriodResult(
    string Currency,
    DateOnly PeriodStart,
    decimal IncomeAmount,
    decimal ExpenseAmount,
    int TransactionCount);
