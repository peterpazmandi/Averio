using Averio.Application.Households.Invitations;
using Averio.Contracts.Households;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Averio.Api.Security;
using Averio.Api.Mapping;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1")]
public sealed class InvitationsController(InvitationHandler handler, HouseholdHistoryHandler history) : ControllerBase
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
    // These authenticated reads never reveal whether an arbitrary address is registered.
    [HttpGet("invitations/pending")]
    public async Task<ActionResult<IReadOnlyList<PendingInvitation>>> Pending(CancellationToken ct) => Ok(await handler.PendingAsync(ct));
    [HttpGet("household-history")]
    public async Task<ActionResult<IReadOnlyList<HistorySummary>>> History(CancellationToken ct) => Ok(await history.ListAsync(ct));
    [HttpGet("household-history/{id:guid}")]
    public async Task<ActionResult<HistorySnapshot>> Historical(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => Ok(await history.GetAsync(id, from, to, ct));
    [HttpGet("households/{householdId:guid}/former-members")]
    public async Task<ActionResult<IReadOnlyList<FormerMember>>> FormerMembers(Guid householdId, CancellationToken ct) => Ok(await history.FormerMembersAsync(householdId, ct));
    [HttpPost("invitations/declinations")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Decline(InvitationTokenRequest input, CancellationToken ct)
    { await handler.DeclineAsync(input.Token, input.InvitationId, ct); return NoContent(); }
    [HttpPost("invitations/preview")]
    public async Task<ActionResult<InvitationPreviewResponse>> Preview(InvitationTokenRequest input, CancellationToken ct)
    {
        var r = await handler.PreviewAsync(input.Token, input.InvitationId, ct);
        var d = r.Departure;
        return Ok(new InvitationPreviewResponse(r.HouseholdName, r.AlreadyJoined, r.CanJoin, d is null ? null : new(
            d.HouseholdId, d.HouseholdName, d.Version, d.WillArchive, d.RequiresSuccessor, d.AccountsStaying,
            d.Accounts.Select(x => new DepartureAccountResponse(x.Id, x.Name, x.Currency, x.Type, x.Version, DecimalAmountMapper.Format(x.Balance))).ToArray(),
            d.Successors.Select(x => new DepartureMemberResponse(x.Id, x.DisplayName)).ToArray())));
    }
    [HttpPost("invitations/acceptances")]
    public async Task<ActionResult<AcceptInvitationResponse>> Accept(AcceptInvitationRequest input, CancellationToken ct)
        => Ok(new AcceptInvitationResponse(await handler.AcceptAsync(new InvitationAcceptance(input.Token, input.InvitationId,
            input.ExpectedSourceVersion, input.Confirmed, input.SuccessorMemberId,
            input.Accounts?.Select(x => new AccountCutoverChoice(x.AccountId, x.ExpectedVersion, DecimalAmountMapper.ParseBalance(x.OpeningBalance), x.Handling)).ToArray() ?? []), ct)));
}
