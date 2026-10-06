using Averio.Application.Households.Invitations;
using Averio.Contracts.Households;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Averio.Api.Security;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1")]
public sealed class InvitationsController(InvitationHandler handler) : ControllerBase
{
    [HttpGet("households/{householdId:guid}/invitations")]
    public async Task<ActionResult<IReadOnlyList<InvitationResponse>>> List(Guid householdId, CancellationToken ct)
        => Ok((await handler.ListAsync(householdId, ct)).Select(x => new InvitationResponse(x.Id, x.Email,
            x.State, x.DeliveryStatus, x.Version, x.ExpiresAt, x.LastSentAt)).ToArray());
    [HttpPost("households/{householdId:guid}/invitations")]
    [EnableRateLimiting(RateLimiterPolicies.PasswordResetRequests)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Create(Guid householdId, CreateInvitationRequest input, CancellationToken ct)
    { await handler.SendAsync(householdId, new(input.Email), null, null, ct); return NoContent(); }
    [HttpPost("households/{householdId:guid}/invitations/{invitationId:guid}/resends")]
    [EnableRateLimiting(RateLimiterPolicies.PasswordResetRequests)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Resend(Guid householdId, Guid invitationId, InvitationVersionRequest input, CancellationToken ct)
    { await handler.SendAsync(householdId, null, invitationId, input.ExpectedVersion, ct); return NoContent(); }
    [HttpPost("households/{householdId:guid}/invitations/{invitationId:guid}/revocations")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid householdId, Guid invitationId, InvitationVersionRequest input, CancellationToken ct)
    { await handler.RevokeAsync(householdId, invitationId, input.ExpectedVersion, ct); return NoContent(); }
    // Tokens stay out of request URLs/access logs. Even preview requires verified matching identity.
    [HttpPost("invitations/preview")]
    public async Task<ActionResult<InvitationPreviewResponse>> Preview(InvitationTokenRequest input, CancellationToken ct)
    { var result = await handler.PreviewAsync(input.Token, ct); return Ok(new InvitationPreviewResponse(result.HouseholdName, result.AlreadyJoined, result.CanJoin)); }
    [HttpPost("invitations/acceptances")]
    public async Task<ActionResult<AcceptInvitationResponse>> Accept(InvitationTokenRequest input, CancellationToken ct)
        => Ok(new AcceptInvitationResponse(await handler.AcceptAsync(input.Token, ct)));
}
