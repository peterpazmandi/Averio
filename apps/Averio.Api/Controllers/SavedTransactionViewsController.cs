using Averio.Application.Transactions.SavedViews;
using Averio.Contracts.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace Averio.Api.Controllers;

[Authorize, ApiController, Route("api/v1/households/{householdId:guid}/saved-transaction-views")]
public sealed class SavedTransactionViewsController(SavedViewHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SavedTransactionViewResponse>>> List(Guid householdId, CancellationToken ct)
        => Ok((await handler.ListAsync(householdId, ct)).Select(x => new SavedTransactionViewResponse(x.Id, x.Name,
            new(x.Filters.Account, x.Filters.Kind, x.Filters.Period, x.Filters.From, x.Filters.To, x.Filters.Activity, x.Filters.Labels), x.Version, x.Available)));
    [HttpPost]
    public async Task<ActionResult<Guid>> Create(Guid householdId, SaveTransactionViewRequest request, CancellationToken ct)
        => Ok(await handler.SaveAsync(householdId, null, null, Input(request), ct));
    [HttpPut("{viewId:guid}")]
    public async Task<IActionResult> Update(Guid householdId, Guid viewId, SaveTransactionViewRequest request, CancellationToken ct)
    {
        await handler.SaveAsync(householdId, viewId, request.ExpectedVersion, Input(request), ct); return NoContent();
    }
    [HttpDelete("{viewId:guid}")]
    public async Task<IActionResult> Delete(Guid householdId, Guid viewId, [FromQuery, BindRequired] Guid expectedVersion, CancellationToken ct)
    {
        await handler.DeleteAsync(householdId, viewId, expectedVersion, ct); return NoContent();
    }
    private static SavedViewInput Input(SaveTransactionViewRequest r)
        => new(r.Name, new(r.Filters.Account, r.Filters.Kind, r.Filters.Period, r.Filters.From, r.Filters.To, r.Filters.Activity, r.Filters.Labels));
}
