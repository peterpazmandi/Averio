using System.Security.Claims;
using Averio.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize, ApiController, Route("api/v1/notifications")]
public sealed class NotificationsController(INotificationInbox inbox) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Lists the signed-in user's currently authorized active notifications.</summary>
    [HttpGet]
    public Task<IReadOnlyList<NotificationItem>> List([FromQuery] Guid householdId, [FromQuery] bool unreadOnly, CancellationToken ct)
        => inbox.ListAsync(UserId, householdId, unreadOnly, ct);

    /// <summary>Counts authorized, active unread notifications for the signed-in user.</summary>
    [HttpGet("unread-count")]
    public async Task<UnreadNotificationCount> Count([FromQuery] Guid householdId, CancellationToken ct)
        => new(await inbox.UnreadCountAsync(UserId, householdId, ct));

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
        => await inbox.MarkReadAsync(UserId, id, ct) ? NoContent() : NotFound();

    [HttpPut("read-all")]
    public async Task<IActionResult> ReadAll([FromQuery] Guid householdId, CancellationToken ct)
    {
        await inbox.MarkAllReadAsync(UserId, householdId, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct)
        => await inbox.DismissAsync(UserId, id, ct) ? NoContent() : NotFound();
}

public sealed record UnreadNotificationCount(int Count);
