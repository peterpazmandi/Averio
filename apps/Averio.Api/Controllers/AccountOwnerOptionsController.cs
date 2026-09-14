using Averio.Application.Accounts.GetAccountOwnerOptions;
using Averio.Contracts.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/account-owner-options")]
public sealed class AccountOwnerOptionsController(GetAccountOwnerOptionsHandler handler)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AccountOwnerOptionsResponse>> Get(Guid householdId, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(householdId, cancellationToken);
        return Ok(new AccountOwnerOptionsResponse(
            result.CurrentMemberId,
            result.Members.Select(member => new AccountOwnerResponse(
                member.HouseholdMemberId, member.DisplayName)).ToArray()));
    }
}
