namespace Averio.Contracts.Statistics;

public sealed record CashFlowResponse(
    DateOnly From,
    DateOnly To,
    string Period,
    IReadOnlyList<CashFlowPeriodResponse> Periods);

public sealed record CashFlowPeriodResponse(
    string Currency,
    DateOnly PeriodStart,
    string IncomeAmount,
    string ExpenseAmount,
    int TransactionCount);
