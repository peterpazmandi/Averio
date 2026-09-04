using Averio.Contracts.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[ApiController]
[Route("api/v1/security")]
public sealed class SecurityController(IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
    public ActionResult<CsrfTokenResponse>
        GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        return Ok(new CsrfTokenResponse(tokens.RequestToken!));
    }
}