using Averio.Api.Mapping;
using Averio.Application.Activities;
using Averio.Contracts.Activities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/activities")]
public sealed class ActivitiesController(ActivityHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ActivityResponse>>> List(Guid householdId, CancellationToken ct)
        => Ok((await handler.ListAsync(householdId, ct)).Select(x => new ActivityResponse(
            x.Activity.Id, x.Activity.Name, x.Activity.StartsOn, x.Activity.EndsOn, x.Activity.IsClosed, x.Activity.UpdatedAt,
            x.Totals.Select(t => new ActivityCurrencyResponse(t.Currency, DecimalAmountMapper.Format(t.Income),
                DecimalAmountMapper.Format(t.Expenses), DecimalAmountMapper.Format(t.Net), t.TransactionCount)).ToArray())).ToArray());
    [HttpPost]
    public async Task<ActionResult<CreateActivityResponse>> Create(Guid householdId, CreateActivityRequest request, CancellationToken ct)
    {
        var id = await handler.CreateAsync(householdId, new ActivityInput(request.Name, request.StartsOn, request.EndsOn), ct);
        return StatusCode(StatusCodes.Status201Created, new CreateActivityResponse(id));
    }
    [HttpGet("{activityId:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyList<ActivityDayResponse>>> Timeline(Guid householdId, Guid activityId, CancellationToken ct)
        => Ok((await handler.TimelineAsync(householdId, activityId, ct)).Select(x => new ActivityDayResponse(
            x.Date, x.Currency, DecimalAmountMapper.Format(x.Income), DecimalAmountMapper.Format(x.Expenses), x.TransactionCount)).ToArray());
    [HttpPut("{activityId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid householdId, Guid activityId, UpdateActivityRequest request, CancellationToken ct)
    {
        await handler.UpdateAsync(householdId, activityId,
            new ActivityInput(request.Name, request.StartsOn, request.EndsOn, request.IsClosed), request.ExpectedUpdatedAt, ct);
        return NoContent();
    }
    [HttpPut("assignments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Assign(Guid householdId, AssignActivitiesRequest request, CancellationToken ct)
    {
        await handler.AssignAsync(householdId, request.ActivityId,
            request.Transactions.Select(x => new ActivityAssignmentChange(x.TransactionId, x.ExpectedVersion)).ToArray(), ct);
        return NoContent();
    }
}
