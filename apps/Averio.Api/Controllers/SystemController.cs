using Averio.Contracts.Diagnostics;
using Averio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Averio.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(
    AverioDbContext dbContext)
    : ControllerBase
{
    [HttpGet("health", Name = "GetSystemHealth")]
    [ProducesResponseType<SystemHealthResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<SystemHealthResponse>(
        StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SystemHealthResponse>> GetHealth(
        CancellationToken cancellationToken)
    {
        var databaseConnected =
            await dbContext.Database.CanConnectAsync(
                cancellationToken);

        var response = new SystemHealthResponse(
            databaseConnected
                ? "healthy"
                : "degraded",
            databaseConnected,
            DateTimeOffset.UtcNow);

        if (!databaseConnected)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response);
        }

        return Ok(response);
    }
}