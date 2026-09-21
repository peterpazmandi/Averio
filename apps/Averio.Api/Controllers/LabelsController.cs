using Averio.Api.Mapping;
using Averio.Application.Labels.CreateLabel;
using Averio.Application.Labels.GetLabels;
using Averio.Contracts.Labels;
using Averio.Domain.Labels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/labels")]
public sealed class LabelsController(
    GetLabelsHandler getHandler,
    CreateLabelHandler createHandler)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LabelResponse>>> List(
        Guid householdId,
        [FromQuery] Guid? accountId,
        CancellationToken cancellationToken)
    {
        var result = await getHandler.HandleAsync(
            householdId,
            accountId,
            cancellationToken);

        return Ok(result.Select(label => new LabelResponse(
            label.Id,
            label.Name,
            label.Scope.ToString(),
            label.AccountId)));
    }

    [HttpPost]
    public async Task<ActionResult<CreateLabelResponse>> Create(
        Guid householdId,
        CreateLabelRequest request,
        CancellationToken cancellationToken)
    {
        var scope = EnumParser.Parse<LabelScope>(
            request.Scope,
            nameof(request.Scope),
            "Validation_TransactionLabelScopeInvalid");

        var id = await createHandler.HandleAsync(
            new CreateLabelCommand(
                householdId,
                request.Name,
                scope,
                request.AccountId),
            cancellationToken);

        return Ok(new CreateLabelResponse(id));
    }
}
