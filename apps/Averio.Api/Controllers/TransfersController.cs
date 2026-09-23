using Averio.Api.Mapping;
using Averio.Application.Transfers;
using Averio.Contracts.Transfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/transfers")]
public sealed class TransfersController(TransferHandler handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateTransferResponse>> Create(Guid householdId,
        CreateTransferRequest request, CancellationToken ct)
    {
        var id = await handler.CreateAsync(householdId, request.SourceAccountId, request.DestinationAccountId,
            Input(request.SentAmount, request.ReceivedAmount, request.OccurredOn, request.Note), ct);
        return CreatedAtAction(nameof(Get), new { householdId, transferId = id }, new CreateTransferResponse(id));
    }

    [HttpGet("{transferId:guid}")]
    public async Task<ActionResult<TransferResponse>> Get(Guid householdId, Guid transferId, CancellationToken ct)
        => Ok(Map(await handler.GetAsync(householdId, transferId, ct)));

    [HttpGet]
    public async Task<ActionResult<TransferPageResponse>> List(Guid householdId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? accountId,
        [FromQuery] int pageSize = 40, [FromQuery] DateOnly? cursorDate = null,
        [FromQuery] DateTimeOffset? cursorCreatedAt = null, [FromQuery] Guid? cursorId = null,
        CancellationToken ct = default)
    {
        var page = await handler.ListAsync(householdId,
            new TransferQuery(from, to, accountId, pageSize, cursorDate, cursorCreatedAt, cursorId), ct);
        return Ok(new TransferPageResponse(page.Items.Select(Map).ToArray(), page.HasMore));
    }

    [HttpPut("{transferId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid householdId, Guid transferId,
        UpdateTransferRequest request, CancellationToken ct)
    {
        await handler.UpdateAsync(householdId, transferId,
            Input(request.SentAmount, request.ReceivedAmount, request.OccurredOn, request.Note),
            request.ExpectedUpdatedAt, ct);
        return NoContent();
    }

    [HttpDelete("{transferId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Void(Guid householdId, Guid transferId,
        [FromQuery, BindRequired] DateTimeOffset expectedUpdatedAt, CancellationToken ct)
    {
        await handler.VoidAsync(householdId, transferId, expectedUpdatedAt, ct);
        return NoContent();
    }

    private static TransferInput Input(string sent, string received, DateOnly date, string? note)
        => new(DecimalAmountMapper.ParseTransactionAmount(sent),
            DecimalAmountMapper.ParseTransactionAmount(received), date, note);

    private static TransferResponse Map(TransferResult result)
        => new(result.Id, Side(result.Source), Side(result.Destination), result.OccurredOn,
            result.Note, result.CreatedAt, result.UpdatedAt, result.IsVoided, result.CanModify);

    private static TransferSideResponse? Side(TransferSide? side)
        => side is null ? null : new(side.AccountId, side.AccountName, side.Currency, DecimalAmountMapper.Format(side.Amount));
}
