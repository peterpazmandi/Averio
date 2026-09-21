using Averio.Domain.Transactions;

namespace Averio.Application.Common.Abstractions;

public interface ITransactionAggregateRepository
{
    Task ApplyDeltaAsync(
        Guid householdId,
        Guid accountId,
        TransactionAggregateDimension dimensionType,
        Guid dimensionId,
        string currency,
        DateOnly occurredOn,
        TransactionKind kind,
        decimal amountDelta,
        int entryCountDelta,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CashFlowAggregateReadModel>> GetCashFlowAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        TransactionAggregatePeriod period,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryCashFlowAggregateReadModel>> GetCategoryCashFlowAsync(
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

public sealed record CategoryCashFlowAggregateReadModel(
    Guid CategoryId,
    string? CategorySystemKey,
    string? CategoryName,
    string CategoryIcon,
    string Currency,
    DateOnly PeriodStart,
    decimal IncomeAmount,
    decimal ExpenseAmount,
    int ItemCount);
