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

        // The database removes inaccessible/non-reporting accounts first. The
        // remaining row count is bounded by accounts × requested periods, so
        // final currency grouping in memory avoids provider-specific value-object
        // translation without falling back to the raw transaction ledger.
        var rows = await (
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
            select aggregate)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => new { row.Currency, row.PeriodStart })
            .OrderBy(group => group.Key.PeriodStart)
            .ThenBy(group => group.Key.Currency.Value)
            .Select(group => new CashFlowAggregateReadModel(
                group.Key.Currency.Value,
                group.Key.PeriodStart,
                group.Sum(row => row.IncomeAmount),
                group.Sum(row => row.ExpenseAmount),
                group.Sum(row => row.TransactionCount)))
            .ToArray();
    }

    private async Task ApplyPeriodAsync(
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

        if (countDelta > 0)
        {
            // Positive contributions are safe to INSERT and PostgreSQL's
            // ON CONFLICT path serializes concurrent updates to the same bucket.
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
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
            return;
        }

        // A negative delta is always the reversal of an existing posted
        // contribution. UPDATE-only avoids proposing a negative INSERT row,
        // while the CHECK constraint protects the aggregate from underflow.
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE transaction_aggregates
            SET
                income_amount = income_amount + {incomeDelta},
                expense_amount = expense_amount + {expenseDelta},
                transaction_count = transaction_count + {countDelta},
                updated_at = {now}
            WHERE household_id = {householdId}
              AND account_id = {accountId}
              AND currency_code = {currency}
              AND period = {periodName}
              AND period_start = {periodStart};
            """, cancellationToken);

        if (affected != 1)
        {
            throw new InvalidOperationException(
                "Expected transaction aggregate contribution was not found.");
        }
    }
}
