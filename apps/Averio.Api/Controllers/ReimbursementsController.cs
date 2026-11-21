using Averio.Api.Mapping;
using Averio.Application.Recurring;
using Averio.Contracts.Recurring;
using Averio.Domain.Recurring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Averio.Api.Controllers;

[Authorize, ApiController, Route("api/v1/households/{householdId:guid}/reimbursements")]
public sealed class ReimbursementsController(ReimbursementHandler handler, ReimbursementPaymentHandler payments, TimeProvider clock) : ControllerBase
{
    [HttpGet("payment-recovery")]
    public async Task<ActionResult<IReadOnlyList<ReimbursementRecoveryLinkResponse>>> RecoveryLinks(Guid householdId, CancellationToken ct)
        => Ok((await payments.RecoveryLinksAsync(householdId, ct)).Select(p => new ReimbursementRecoveryLinkResponse(p.ClaimId, p.PaymentId)).ToArray());
    [HttpPost("payment-recovery")]
    public async Task<IActionResult> Recover(Guid householdId, RecoverReimbursementPaymentsRequest request, CancellationToken ct)
    {
        await payments.RecoverAsync(householdId, request.Links.Select(p => new ReimbursementRecoveryLink(p.ClaimId, p.PaymentId)).ToArray(), ct);
        return NoContent();
    }
    [HttpPost]
    public async Task<ActionResult<CreateReimbursementClaimResponse>> Create(Guid householdId, CreateReimbursementClaimRequest request, CancellationToken ct)
    {
        var configuration = ReimbursementConfigurationMapper.Parse(request.Configuration)
            ?? throw new Averio.Application.Common.Exceptions.BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement");
        return Ok(new CreateReimbursementClaimResponse(await handler.CreateAsync(householdId, request.TransactionId, request.ExpectedUpdatedAt, configuration, ct)));
    }
    [HttpPut("{claimId:guid}")]
    public async Task<IActionResult> Update(Guid householdId, Guid claimId, UpdateReimbursementClaimRequest request, CancellationToken ct)
    {
        var configuration = ReimbursementConfigurationMapper.Parse(request.Configuration)
            ?? throw new Averio.Application.Common.Exceptions.BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement");
        await handler.UpdateAsync(householdId, claimId, request.ExpectedVersion, configuration, ct);
        return NoContent();
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReimbursementClaimResponse>>> List(Guid householdId, [FromQuery] Guid? transactionId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return Ok((await handler.ListAsync(householdId, transactionId, ct)).Select(x => {
            var c = x.Claim;
            var expected = x.PaymentUnavailable ? 0 : c.ExpectedAmount;
            return new ReimbursementClaimResponse(c.Id, c.TransactionId, x.Description, c.Provider, c.Currency,
                DecimalAmountMapper.Format(c.PotentialAmount), DecimalAmountMapper.Format(expected), DecimalAmountMapper.Format(c.ReceivedAmount), DecimalAmountMapper.Format(x.PaymentUnavailable ? 0 : c.OutstandingAmount),
                DecimalAmountMapper.Format(c.OriginalPaymentAmount), c.PaymentOn, c.DestinationAccountId, c.Status.ToString(),
                c.SubmissionReminderOn, c.SubmittedOn, c.ExpectedOn, c.Reference, c.Version, x.CanModify, x.PaymentUnavailable, x.PaymentChanged,
                !x.PaymentUnavailable && c.Status == ReimbursementStatus.NeedsSubmission && c.SubmissionReminderOn <= today,
                !x.PaymentUnavailable && c.Status is (ReimbursementStatus.Submitted or ReimbursementStatus.PartiallyReceived) && c.OutstandingAmount > 0 && c.ExpectedOn < today,
                c.Events.OrderBy(e => e.RecordedAt).ThenBy(e => e.Sequence).Select(e => new ReimbursementEventResponse(e.Status.ToString(), e.SubmittedOn, e.Reference, e.RecordedAt,
                    e.ChangeKind, e.Provider, e.PotentialAmount is decimal amount ? DecimalAmountMapper.Format(amount) : null, e.DestinationAccountId,
                    e.PaymentDelayDays, e.SubmissionReminderOn, e.ExpectedOn)).ToArray(), c.PaymentDelayDays,
                c.SubmissionReminderOn is DateOnly reminder ? reminder.DayNumber - DateOnly.FromDateTime(c.CreatedAt.UtcDateTime).DayNumber : null,
                c.Payments.OrderBy(p => p.LinkedAt).ThenBy(p => p.Id).Select(p => new ReimbursementPaymentResponse(p.Id, p.CreditTransactionId, DecimalAmountMapper.Format(p.Amount), p.ReceivedOn, p.LinkedAt, p.UnlinkedAt)).ToArray());
        }).ToArray());
    }
    [HttpGet("{claimId:guid}/payment-candidates")]
    public async Task<ActionResult<ReimbursementPaymentCandidatesResponse>> Candidates(Guid householdId, Guid claimId, [FromQuery] string? search, [FromQuery] int offset = 0, CancellationToken ct = default)
    {
        var rows = await payments.CandidatesAsync(householdId, claimId, search, offset, ct);
        return Ok(new ReimbursementPaymentCandidatesResponse(rows.Take(20).Select(t => new ReimbursementPaymentCandidateResponse(t.Id, t.Description, DecimalAmountMapper.Format(t.Amount), t.OccurredOn, t.UpdatedAt)).ToArray(), rows.Count > 20));
    }
    [HttpPost("{claimId:guid}/payments")]
    public async Task<ActionResult<LinkReimbursementPaymentResponse>> LinkPayment(Guid householdId, Guid claimId, LinkReimbursementPaymentRequest request, CancellationToken ct)
        => Ok(new LinkReimbursementPaymentResponse(await payments.LinkAsync(householdId, claimId,
            new(request.CreditTransactionId, request.OperationId, request.ExpectedVersion, request.ExpectedCreditUpdatedAt, request.MoneyReceived), ct)));
    [HttpPost("{claimId:guid}/payments/{paymentId:guid}/unlink")]
    public async Task<IActionResult> UnlinkPayment(Guid householdId, Guid claimId, Guid paymentId, UnlinkReimbursementPaymentRequest request, CancellationToken ct)
    {
        await payments.UnlinkAsync(householdId, claimId, paymentId, request.ExpectedVersion, ct);
        return NoContent();
    }
    [HttpPut("{claimId:guid}/status")]
    public async Task<IActionResult> Change(Guid householdId, Guid claimId, ChangeReimbursementRequest request, CancellationToken ct)
    {
        await handler.ChangeAsync(householdId, claimId, request.ExpectedVersion, request.ReceiptSubmitted, request.SubmittedOn,
            request.Reference, EnumParser.Parse<ReimbursementStatus>(request.Status, nameof(request.Status), "Validation_Reimbursement"), ct);
        return NoContent();
    }
}
