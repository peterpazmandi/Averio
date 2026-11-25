using Averio.Api.Mapping;
using Averio.Application.Recurring;
using Averio.Contracts.Recurring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Averio.Api.Controllers;
[Authorize, ApiController, Route("api/v1/households/{householdId:guid}/money-back")]
public sealed class MoneyBackController(MoneyBackHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MoneyBackPageResponse>> List(Guid householdId, [FromQuery] string? status, [FromQuery] int offset = 0, [FromQuery] int limit = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(householdId, offset, limit, status, ct);
        return Ok(new MoneyBackPageResponse(result.Items.Select(x => new MoneyBackItemResponse(x.Kind, x.Id, x.Provider ?? x.Merchant ?? "", x.Provider, x.Merchant, x.Currency, DecimalAmountMapper.Format(x.Amount), x.Outstanding is decimal o ? DecimalAmountMapper.Format(o) : null, x.Date, x.Status, x.SourceId, x.CreditId, x.CanModify)).ToArray(), result.HasMore, offset, Math.Clamp(limit, 1, 100)));
    }
}
