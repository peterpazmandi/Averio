using Averio.Api.Mapping;
using Averio.Application.Transactions.Refunds;
using Averio.Contracts.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Averio.Api.Controllers;
[ApiController, Authorize]
[Route("api/v1/households/{householdId:guid}/transactions/{transactionId:guid}/refunds")]
public sealed class ItemRefundsController(ItemRefundHandler handler) : ControllerBase
{
    [HttpGet("origin")]
    public async Task<ActionResult<ItemRefundResponse>> Origin(Guid householdId, Guid transactionId, CancellationToken ct)
    {
        var x = await handler.OriginAsync(householdId, transactionId, ct);
        return Ok(new ItemRefundResponse(x.Id, x.TransactionId, x.TransactionItemId, x.CreditTransactionId,
            DecimalAmountMapper.Format(x.Amount), x.ReceivedOn, x.CreatedAt, x.CancelledAt, x.Version));
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ItemRefundResponse>>> List(Guid householdId, Guid transactionId, CancellationToken ct)
        => Ok((await handler.ListAsync(householdId, transactionId, ct)).Select(x => new ItemRefundResponse(x.Id, x.TransactionId,
            x.TransactionItemId, x.CreditTransactionId, DecimalAmountMapper.Format(x.Amount), x.ReceivedOn, x.CreatedAt, x.CancelledAt, x.Version)).ToArray());
    [HttpPost]
    public async Task<ActionResult<RecordItemRefundResponse>> Record(Guid householdId, Guid transactionId, RecordItemRefundRequest request, CancellationToken ct)
        => Ok(new RecordItemRefundResponse(await handler.RecordAsync(householdId, transactionId,
            new(request.TransactionItemId, DecimalAmountMapper.ParseTransactionAmount(request.Amount), request.ReceivedOn, request.OperationId,
                request.ExpectedUpdatedAt, request.MoneyReceived, request.CreditTransactionId, request.ExpectedCreditUpdatedAt), ct)));
    [HttpPost("{refundId:guid}/cancellation")]
    public async Task<IActionResult> Cancel(Guid householdId, Guid transactionId, Guid refundId, CancelItemRefundRequest request, CancellationToken ct)
    {
        await handler.CancelAsync(householdId, transactionId, refundId, request.ExpectedVersion, request.ExpectedUpdatedAt, ct);
        return NoContent();
    }
}
