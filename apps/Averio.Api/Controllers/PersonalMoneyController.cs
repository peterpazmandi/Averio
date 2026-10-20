using Averio.Api.Mapping;
using Averio.Application.PersonalMoney;
using Averio.Contracts.PersonalMoney;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/personal-money")]
public sealed class PersonalMoneyController(PersonalMoneyHandler handler) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<PersonalMoneySnapshotResponse>> Mine(
        Guid householdId, CancellationToken ct)
    {
        var result = await handler.GetMineAsync(householdId, ct);
        if (result is null) return NoContent();
        return Ok(new PersonalMoneySnapshotResponse(result.WalletId, result.Currency,
            DecimalAmountMapper.Format(result.Available),
            DecimalAmountMapper.Format(result.AttributedPrivateFunding),
            DecimalAmountMapper.Format(result.FundingDifference),
            result.Entries.Select(x => new PersonalMoneyEventResponse(x.Id, x.Kind,
                DecimalAmountMapper.Format(x.Amount), x.OccurredOn,
                x.TransactionId, x.TransferId, x.RelatedTransactionId)).ToArray(),
            result.Charges.Select(x => new PersonalMoneyChargeResponse(x.TransactionId,
                DecimalAmountMapper.Format(x.Amount), x.Version)).ToArray()));
    }

    [HttpPut("me/charges/{transactionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CorrectCharge(Guid householdId, Guid transactionId,
        CorrectPersonalMoneyChargeRequest request, CancellationToken ct)
    {
        await handler.CorrectChargeAsync(householdId, transactionId,
            DecimalAmountMapper.ParseTransactionAmount(request.Amount),
            DecimalAmountMapper.ParseTransactionAmount(request.ExpectedAmount),
            request.ExpectedVersion, request.OperationId, ct);
        return NoContent();
    }

    [HttpPost("me/refunds")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Refund(Guid householdId, RefundPersonalMoneyRequest request, CancellationToken ct)
    {
        await handler.RefundAsync(householdId, request.RefundTransactionId,
            request.PurchaseTransactionId, DecimalAmountMapper.ParseTransactionAmount(request.Amount),
            request.OperationId, ct);
        return NoContent();
    }

    [HttpPost("me/surrenders")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Surrender(Guid householdId, SurrenderPersonalMoneyRequest request, CancellationToken ct)
    {
        await handler.SurrenderAsync(householdId, DecimalAmountMapper.ParseTransactionAmount(request.Amount),
            request.OccurredOn, request.OperationId, ct);
        return NoContent();
    }

    [HttpPost("grants")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Grant(Guid householdId,
        GrantPersonalMoneyRequest request, CancellationToken ct)
    {
        await handler.GrantAsync(householdId, request.RecipientMemberId,
            request.Currency, DecimalAmountMapper.ParseTransactionAmount(request.Amount),
            request.OccurredOn, request.OperationId, ct);
        return NoContent();
    }

    [HttpPost("me/charges")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Charge(Guid householdId,
        ChargePersonalMoneyRequest request, CancellationToken ct)
    {
        await handler.ChargePrivateAsync(householdId, request.TransactionId,
            DecimalAmountMapper.ParseTransactionAmount(request.Amount), request.OperationId, ct);
        return NoContent();
    }
}
