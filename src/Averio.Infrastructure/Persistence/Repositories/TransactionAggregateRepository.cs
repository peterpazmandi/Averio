using Averio.Application.Common.Abstractions;
using Averio.Domain.Accounts;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class TransactionAggregateRepository(
    AverioDbContext dbContext,
    TimeProvider timeProvider)
    : ITransactionAggregateRepository
{
    public async Task ApplyDeltaAsync(
        Guid householdId,
        Guid accountId,
        string currency,
        DateOnly occurredOn,
        TransactionKind kind,
        decimal amountDelta,
        int countDelta,
        CancellationToken cancellationToken)
    {
        var incomeDelta = kind == TransactionKind.Income ? amountDelta : 0m;
        var expenseDelta = kind == TransactionKind.Expense ? amountDelta : 0m;
        var now = timeProvider.GetUtcNow();

        await ApplyPeriodAsync(
            householdId, accountId, currency, TransactionAggregatePeriod.Day,
            occurredOn, incomeDelta, expenseDelta, countDelta, now, cancellationToken);

        await ApplyPeriodAsync(
            householdId, accountId, currency, TransactionAggregatePeriod.Month,
            new DateOnly(occurredOn.Year, occurredOn.Month, 1),
            incomeDelta, expenseDelta, countDelta, now, cancellationToken);
    }

    public async Task<IReadOnlyList<CashFlowAggregateReadModel>> GetCashFlowAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        TransactionAggregatePeriod period,
        CancellationToken cancellationToken)
    {
        var periodFrom = period == TransactionAggregatePeriod.Month
            ? new DateOnly(from.Year, from.Month, 1)
            : from;
        var periodTo = period == TransactionAggregatePeriod.Month
            ? new DateOnly(to.Year, to.Month, 1)
            : to;

        return await (
            from aggregate in dbContext.TransactionAggregates.AsNoTracking()
            join account in dbContext.Accounts.AsNoTracking()
                on new { aggregate.HouseholdId, aggregate.AccountId }
                equals new { account.HouseholdId, AccountId = account.Id }
            where aggregate.HouseholdId == householdId
                && aggregate.Period == period
                && aggregate.PeriodStart >= periodFrom
                && aggregate.PeriodStart <= periodTo
                && account.Visibility == AccountVisibility.Household
                && account.IncludeInHouseholdReports
            group aggregate by new
            {
                Currency = aggregate.Currency.Value,
                aggregate.PeriodStart,
            }
            into grouped
            orderby grouped.Key.PeriodStart, grouped.Key.Currency
            select new CashFlowAggregateReadModel(
                grouped.Key.Currency,
                grouped.Key.PeriodStart,
                grouped.Sum(row => row.IncomeAmount),
                grouped.Sum(row => row.ExpenseAmount),
                grouped.Sum(row => row.TransactionCount)))
            .ToListAsync(cancellationToken);
    }

    private Task ApplyPeriodAsync(
        Guid householdId,
        Guid accountId,
        string currency,
        TransactionAggregatePeriod period,
        DateOnly periodStart,
        decimal incomeDelta,
        decimal expenseDelta,
        int countDelta,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var periodName = period.ToString();

        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO transaction_aggregates
                (household_id, account_id, currency_code, period, period_start,
                 income_amount, expense_amount, transaction_count, updated_at)
            VALUES
                ({householdId}, {accountId}, {currency}, {periodName}, {periodStart},
                 {incomeDelta}, {expenseDelta}, {countDelta}, {now})
            ON CONFLICT
                (household_id, account_id, currency_code, period, period_start)
            DO UPDATE SET
                income_amount =
                    transaction_aggregates.income_amount + EXCLUDED.income_amount,
                expense_amount =
                    transaction_aggregates.expense_amount + EXCLUDED.expense_amount,
                transaction_count =
                    transaction_aggregates.transaction_count + EXCLUDED.transaction_count,
                updated_at = EXCLUDED.updated_at;
            """, cancellationToken);
    }
}
