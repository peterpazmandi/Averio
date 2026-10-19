using Averio.Api.Mapping;
using Averio.Application.Statistics.GetCashFlow;
using Averio.Application.Statistics.GetBoundaryCashFlow;
using Averio.Application.Statistics.GetCategoryCashFlow;
using Averio.Contracts.Statistics;
using Averio.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/statistics")]
public sealed class StatisticsController(
    GetCashFlowHandler cashFlowHandler,
    GetCategoryCashFlowHandler categoryCashFlowHandler,
    BoundaryCashFlowHandler boundaryCashFlowHandler)
    : ControllerBase
{
    [HttpGet("cash-flow")]
    public async Task<ActionResult<CashFlowResponse>> GetCashFlow(
        Guid householdId,
        [FromQuery, BindRequired] DateOnly from,
        [FromQuery, BindRequired] DateOnly to,
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

    [HttpGet("boundary-flow")]
    public async Task<ActionResult<BoundaryCashFlowResponse>> GetBoundaryFlow(Guid householdId,
        [FromQuery, BindRequired] DateOnly from, [FromQuery, BindRequired] DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var rows = await boundaryCashFlowHandler.GetAsync(householdId, from, to, cancellationToken);
        return Ok(new BoundaryCashFlowResponse(from, to, rows.Select(x =>
            new BoundaryMonthResponse(x.Currency, x.Month,
                DecimalAmountMapper.Format(x.PayoutsToExcludedAccounts),
                DecimalAmountMapper.Format(x.ReturnsToHousehold), x.TransferCount)).ToArray()));
    }

    [HttpGet("category-cash-flow")]
    public async Task<ActionResult<CategoryCashFlowResponse>> GetCategoryCashFlow(
        Guid householdId,
        [FromQuery, BindRequired] DateOnly from,
        [FromQuery, BindRequired] DateOnly to,
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
