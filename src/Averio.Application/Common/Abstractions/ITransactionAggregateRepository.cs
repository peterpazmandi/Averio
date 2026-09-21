using Averio.Domain.Transactions;

namespace Averio.Application.Common.Abstractions;

public interface ITransactionAggregateRepository
{
    Task ApplyDeltaAsync(
        Guid householdId,
        Guid accountId,
        string currency,
        DateOnly occurredOn,
        TransactionKind kind,
        decimal amountDelta,
        int countDelta,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CashFlowAggregateReadModel>> GetCashFlowAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        TransactionAggregatePeriod period,
        CancellationToken cancellationToken);
}

public sealed record CashFlowAggregateReadModel(
    string Currency,
    DateOnly PeriodStart,
    decimal IncomeAmount,
    decimal ExpenseAmount,
    int TransactionCount);
