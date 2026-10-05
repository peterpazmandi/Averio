using Averio.Application.Households.GetHousehold;
using Averio.Application.Households.UpdateHousehold;
using Averio.Contracts.Households;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households")]
public sealed class HouseholdsController(
    GetHouseholdHandler getHandler,
    UpdateHouseholdHandler updateHandler)
    : ControllerBase
{
    [HttpGet("{householdId:guid}")]
    public async Task<ActionResult<HouseholdResponse>> Get(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var result = await getHandler.HandleAsync(
                householdId,
                cancellationToken);

        return Ok(new HouseholdResponse(
                result.Id,
                result.Name,
                result.CountryCode,
                result.PreferredCurrency,
                result.OnboardingCompleted,
                result.Role,
                result.Version,
                result.Members.Select(member => new HouseholdMemberResponse(
                    member.Id, member.DisplayName, member.Role, member.JoinedAtUtc,
                    member.IsCurrentUser)).ToArray()));
    }

    [HttpPatch("{householdId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid householdId,
        UpdateHouseholdRequest request,
        CancellationToken cancellationToken)
    {
        await updateHandler.HandleAsync(
            new UpdateHouseholdCommand(
                householdId,
                request.Name,
                request.CountryCode,
                request.PreferredCurrency,
                request.ExpectedVersion),
            cancellationToken);

        return NoContent();
    }
}