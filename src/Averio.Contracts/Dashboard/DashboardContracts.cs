using Averio.Contracts.Accounts;
using Averio.Contracts.Transactions;

namespace Averio.Contracts.Dashboard;

public sealed record DashboardResponse(
    string? DisplayName,
    string ReportingCurrency,
    IReadOnlyList<HouseholdBalanceResponse> HouseholdBalances,
    IReadOnlyList<AccountResponse> Accounts,
    IReadOnlyList<DashboardCashFlowPeriodResponse> CashFlow,
    IReadOnlyList<TransactionResponse> LatestTransactions);

public sealed record HouseholdBalanceResponse(
    string Currency,
    string Amount,
    DateOnly EarliestBalanceDate,
    DateOnly LatestBalanceDate);

public sealed record DashboardCashFlowPeriodResponse(
    string Currency,
    DateOnly PeriodStart,
    string IncomeAmount,
    string ExpenseAmount,
    int TransactionCount);
