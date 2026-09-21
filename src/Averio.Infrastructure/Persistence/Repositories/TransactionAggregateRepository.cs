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
        TransactionAggregateDimension dimensionType,
        Guid dimensionId,
        string currency,
        DateOnly occurredOn,
        TransactionKind kind,
        decimal amountDelta,
        int entryCountDelta,
        CancellationToken cancellationToken)
    {
        var incomeDelta = kind == TransactionKind.Income ? amountDelta : 0m;
        var expenseDelta = kind == TransactionKind.Expense ? amountDelta : 0m;
        var now = timeProvider.GetUtcNow();

        await ApplyPeriodAsync(
            householdId, accountId, dimensionType, dimensionId, currency,
            TransactionAggregatePeriod.Day, occurredOn,
            incomeDelta, expenseDelta, entryCountDelta, now, cancellationToken);

        await ApplyPeriodAsync(
            householdId, accountId, dimensionType, dimensionId, currency,
            TransactionAggregatePeriod.Month,
            new DateOnly(occurredOn.Year, occurredOn.Month, 1),
            incomeDelta, expenseDelta, entryCountDelta, now, cancellationToken);

        await ApplyPeriodAsync(
            householdId, accountId, dimensionType, dimensionId, currency,
            TransactionAggregatePeriod.Year,
            new DateOnly(occurredOn.Year, 1, 1),
            incomeDelta, expenseDelta, entryCountDelta, now, cancellationToken);
    }

    public async Task<IReadOnlyList<CashFlowAggregateReadModel>> GetCashFlowAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        TransactionAggregatePeriod period,
        CancellationToken cancellationToken)
    {
        var (periodFrom, periodTo) = NormalizeRange(from, to, period);

        var rows = await (
            from aggregate in dbContext.TransactionAggregates.AsNoTracking()
            join account in dbContext.Accounts.AsNoTracking()
                on new { aggregate.HouseholdId, aggregate.AccountId }
                equals new { account.HouseholdId, AccountId = account.Id }
            where aggregate.HouseholdId == householdId
                && aggregate.DimensionType == TransactionAggregateDimension.Account
                && aggregate.DimensionId == aggregate.AccountId
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
                group.Sum(row => row.EntryCount)))
            .ToArray();
    }

    public async Task<IReadOnlyList<CategoryCashFlowAggregateReadModel>> GetCategoryCashFlowAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        TransactionAggregatePeriod period,
        CancellationToken cancellationToken)
    {
        var (periodFrom, periodTo) = NormalizeRange(from, to, period);

        var rows = await (
            from aggregate in dbContext.TransactionAggregates.AsNoTracking()
            join account in dbContext.Accounts.AsNoTracking()
                on new { aggregate.HouseholdId, aggregate.AccountId }
                equals new { account.HouseholdId, AccountId = account.Id }
            join category in dbContext.Categories.AsNoTracking()
                on aggregate.DimensionId equals category.Id
            where aggregate.HouseholdId == householdId
                && aggregate.DimensionType == TransactionAggregateDimension.Category
                && aggregate.Period == period
                && aggregate.PeriodStart >= periodFrom
                && aggregate.PeriodStart <= periodTo
                && account.Visibility == AccountVisibility.Household
                && account.IncludeInHouseholdReports
            select new
            {
                aggregate,
                category.Name,
                category.Icon,
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => new
            {
                row.aggregate.DimensionId,
                row.Name,
                row.Icon,
                row.aggregate.Currency,
                row.aggregate.PeriodStart,
            })
            .OrderBy(group => group.Key.PeriodStart)
            .ThenByDescending(group => group.Sum(row => row.aggregate.ExpenseAmount))
            .ThenBy(group => group.Key.Name)
            .Select(group => new CategoryCashFlowAggregateReadModel(
                group.Key.DimensionId,
                group.Key.Name,
                group.Key.Icon,
                group.Key.Currency.Value,
                group.Key.PeriodStart,
                group.Sum(row => row.aggregate.IncomeAmount),
                group.Sum(row => row.aggregate.ExpenseAmount),
                group.Sum(row => row.aggregate.EntryCount)))
            .ToArray();
    }

    private async Task ApplyPeriodAsync(
        Guid householdId,
        Guid accountId,
        TransactionAggregateDimension dimensionType,
        Guid dimensionId,
        string currency,
        TransactionAggregatePeriod period,
        DateOnly periodStart,
        decimal incomeDelta,
        decimal expenseDelta,
        int entryCountDelta,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dimensionName = dimensionType.ToString();
        var periodName = period.ToString();

        if (entryCountDelta > 0)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO transaction_aggregates
                    (household_id, account_id, dimension_type, dimension_id,
                     currency_code, period, period_start,
                     income_amount, expense_amount, entry_count, updated_at)
                VALUES
                    ({householdId}, {accountId}, {dimensionName}, {dimensionId},
                     {currency}, {periodName}, {periodStart},
                     {incomeDelta}, {expenseDelta}, {entryCountDelta}, {now})
                ON CONFLICT
                    (household_id, account_id, dimension_type, dimension_id,
                     currency_code, period, period_start)
                DO UPDATE SET
                    income_amount =
                        transaction_aggregates.income_amount + EXCLUDED.income_amount,
                    expense_amount =
                        transaction_aggregates.expense_amount + EXCLUDED.expense_amount,
                    entry_count =
                        transaction_aggregates.entry_count + EXCLUDED.entry_count,
                    updated_at = EXCLUDED.updated_at;
                """, cancellationToken);
            return;
        }

        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE transaction_aggregates
            SET
                income_amount = income_amount + {incomeDelta},
                expense_amount = expense_amount + {expenseDelta},
                entry_count = entry_count + {entryCountDelta},
                updated_at = {now}
            WHERE household_id = {householdId}
              AND account_id = {accountId}
              AND dimension_type = {dimensionName}
              AND dimension_id = {dimensionId}
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

    private static (DateOnly From, DateOnly To) NormalizeRange(
        DateOnly from,
        DateOnly to,
        TransactionAggregatePeriod period)
        => period switch
        {
            TransactionAggregatePeriod.Month => (
                new DateOnly(from.Year, from.Month, 1),
                new DateOnly(to.Year, to.Month, 1)),
            TransactionAggregatePeriod.Year => (
                new DateOnly(from.Year, 1, 1),
                new DateOnly(to.Year, 1, 1)),
            _ => (from, to),
        };
}
