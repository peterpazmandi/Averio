using Averio.Application.Categories.GetIcons;
using Averio.Contracts.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/icons")]
public sealed class IconsController(GetIconsHandler handler)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IconResponse>>> List(
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return Ok(result.Select(icon => new IconResponse(
            icon.Id,
            icon.Name,
            icon.Value,
            icon.SortOrder)));
    }
}
