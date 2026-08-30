#if DEBUG

using Averio.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[ApiController]
[Route("dev/errors")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class DevelopmentErrorsController
    : ControllerBase
{
    [HttpGet("not-found")]
    public IActionResult ThrowNotFound()
    {
        throw new NotFoundException(
            "account",
            Guid.Empty);
    }
}

#endif