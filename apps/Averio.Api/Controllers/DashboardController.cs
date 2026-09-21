using Averio.Api.Mapping;
using Averio.Application.Dashboard.GetDashboard;
using Averio.Contracts.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/dashboard")]
public sealed class DashboardController(
    GetDashboardHandler handler)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            householdId,
            cancellationToken);

        var balances = result.HouseholdBalances
            .Select(balance => new HouseholdBalanceResponse(
                balance.Currency,
                DecimalAmountMapper.Format(balance.Amount),
                balance.EarliestBalanceDate,
                balance.LatestBalanceDate))
            .ToArray();

        var cashFlow = result.CashFlow
            .Select(period => new DashboardCashFlowPeriodResponse(
                period.Currency,
                period.PeriodStart,
                DecimalAmountMapper.Format(period.IncomeAmount),
                DecimalAmountMapper.Format(period.ExpenseAmount),
                period.TransactionCount))
            .ToArray();

        return Ok(new DashboardResponse(
            result.DisplayName,
            result.ReportingCurrency,
            balances,
            AccountResponseMapper.From(result.Accounts),
            cashFlow,
            result.LatestTransactions.Select(TransactionMapper.From).ToArray()));
    }
}
