using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Transactions;
using FluentValidation;
using FluentValidation.Results;

namespace Averio.Application.Statistics.GetCashFlow;

public sealed class GetCashFlowHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    ITransactionAggregateRepository aggregateRepository)
{
    public async Task<CashFlowResult> HandleAsync(
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

        var rows = await aggregateRepository.GetCashFlowAsync(
            household.Id,
            from,
            to,
            period,
            cancellationToken);

        return new CashFlowResult(
            from,
            to,
            period,
            rows.Select(row => new CashFlowPeriodResult(
                row.Currency,
                row.PeriodStart,
                row.IncomeAmount,
                row.ExpenseAmount,
                row.TransactionCount))
                .ToArray());
    }
}

public sealed record CashFlowResult(
    DateOnly From,
    DateOnly To,
    TransactionAggregatePeriod Period,
    IReadOnlyList<CashFlowPeriodResult> Periods);

public sealed record CashFlowPeriodResult(
    string Currency,
    DateOnly PeriodStart,
    decimal IncomeAmount,
    decimal ExpenseAmount,
    int TransactionCount);
