using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Transactions;
using FluentValidation;
using FluentValidation.Results;

namespace Averio.Application.Statistics.GetCategoryCashFlow;

public sealed class GetCategoryCashFlowHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    ITransactionAggregateRepository aggregateRepository)
{
    public async Task<CategoryCashFlowResult> HandleAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        TransactionAggregatePeriod period,
        CancellationToken cancellationToken)
    {
        if (from == default || to == default || from > to)
        {
            const string code = "Validation_CashFlowDateRangeInvalid";
            throw new ValidationException(
            [
                new ValidationFailure("from", code) { ErrorCode = code }
            ]);
        }

        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByIdForUserAsync(
            householdId,
            userId,
            cancellationToken)
            ?? throw new NotFoundException("household", householdId);

        var rows = await aggregateRepository.GetCategoryCashFlowAsync(
            household.Id,
            from,
            to,
            period,
            cancellationToken);

        return new CategoryCashFlowResult(
            from,
            to,
            period,
            rows.Select(row => new CategoryCashFlowPeriodResult(
                row.CategoryId,
                row.CategoryName,
                row.CategoryIcon,
                row.Currency,
                row.PeriodStart,
                row.IncomeAmount,
                row.ExpenseAmount,
                row.ItemCount))
                .ToArray());
    }
}

public sealed record CategoryCashFlowResult(
    DateOnly From,
    DateOnly To,
    TransactionAggregatePeriod Period,
    IReadOnlyList<CategoryCashFlowPeriodResult> Periods);

public sealed record CategoryCashFlowPeriodResult(
    Guid CategoryId,
    string CategoryName,
    string CategoryIcon,
    string Currency,
    DateOnly PeriodStart,
    decimal IncomeAmount,
    decimal ExpenseAmount,
    int ItemCount);
