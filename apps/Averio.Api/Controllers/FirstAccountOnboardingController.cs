using Averio.Api.Mapping;
using Averio.Application.Onboarding.CompleteFirstAccount;
using Averio.Contracts.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/onboarding/first-account")]
public sealed class FirstAccountOnboardingController(CompleteFirstAccountHandler handler) : ControllerBase
{
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Complete(
        Guid householdId,
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = AccountRequestMapper.ToCommand(householdId, request);

        await handler.HandleAsync(command, cancellationToken);

        return NoContent();
    }
}