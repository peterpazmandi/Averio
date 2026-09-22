using Averio.Api.Mapping;
using Averio.Application.Statistics.GetCashFlow;
using Averio.Application.Statistics.GetCategoryCashFlow;
using Averio.Contracts.Statistics;
using Averio.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/statistics")]
public sealed class StatisticsController(
    GetCashFlowHandler cashFlowHandler,
    GetCategoryCashFlowHandler categoryCashFlowHandler)
    : ControllerBase
{
    [HttpGet("cash-flow")]
    public async Task<ActionResult<CashFlowResponse>> GetCashFlow(
        Guid householdId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string period = "Month",
        CancellationToken cancellationToken = default)
    {
        var parsedPeriod = ParsePeriod(period);

        var result = await cashFlowHandler.HandleAsync(
            householdId,
            from,
            to,
            parsedPeriod,
            cancellationToken);

        return Ok(new CashFlowResponse(
            result.From,
            result.To,
            result.Period.ToString(),
            result.Periods.Select(item => new CashFlowPeriodResponse(
                item.Currency,
                item.PeriodStart,
                DecimalAmountMapper.Format(item.IncomeAmount),
                DecimalAmountMapper.Format(item.ExpenseAmount),
                item.TransactionCount))
                .ToArray()));
    }

    [HttpGet("category-cash-flow")]
    public async Task<ActionResult<CategoryCashFlowResponse>> GetCategoryCashFlow(
        Guid householdId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string period = "Month",
        CancellationToken cancellationToken = default)
    {
        var parsedPeriod = ParsePeriod(period);

        var result = await categoryCashFlowHandler.HandleAsync(
            householdId,
            from,
            to,
            parsedPeriod,
            cancellationToken);

        return Ok(new CategoryCashFlowResponse(
            result.From,
            result.To,
            result.Period.ToString(),
            result.Periods.Select(item => new CategoryCashFlowPeriodResponse(
                item.CategoryId,
                item.CategorySystemKey,
                item.CategoryName,
                item.CategoryIcon,
                item.Currency,
                item.PeriodStart,
                DecimalAmountMapper.Format(item.IncomeAmount),
                DecimalAmountMapper.Format(item.ExpenseAmount),
                item.ItemCount))
                .ToArray()));
    }

    private static TransactionAggregatePeriod ParsePeriod(string period)
        => EnumParser.Parse<TransactionAggregatePeriod>(
            period,
            nameof(period),
            "Validation_CashFlowPeriodInvalid");
}
