using Averio.Api.Mapping;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.GetTransaction;
using Averio.Application.Transactions.GetTransactions;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Contracts.Transactions;
using Averio.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/transactions")]
public sealed class TransactionsController(
    CreateTransactionHandler createHandler,
    UpdateTransactionHandler updateHandler,
    VoidTransactionHandler voidHandler,
    GetTransactionHandler getHandler,
    GetTransactionsHandler listHandler)
    : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateTransactionResponse>> Create(
        Guid householdId,
        CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var id = await createHandler.HandleAsync(
            TransactionMapper.ToCommand(householdId, request),
            cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { householdId, transactionId = id },
            new CreateTransactionResponse(id));
    }

    [HttpGet("{transactionId:guid}")]
    public async Task<ActionResult<TransactionResponse>> Get(
        Guid householdId,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var result = await getHandler.HandleAsync(
            householdId,
            transactionId,
            cancellationToken);

        return Ok(TransactionMapper.From(result));
    }

    [HttpGet]
    public async Task<ActionResult<TransactionPageResponse>> List(
        Guid householdId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? accountId,
        [FromQuery] string? kind,
        [FromQuery] string? search,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateOnly? cursorDate = null,
        [FromQuery] DateTimeOffset? cursorCreatedAt = null,
        [FromQuery] Guid? cursorId = null,
        CancellationToken cancellationToken = default)
    {
        TransactionKind? parsedKind = string.IsNullOrWhiteSpace(kind)
            ? null
            : EnumParser.Parse<TransactionKind>(
                kind,
                nameof(kind),
                "Validation_TransactionKindInvalid");

        var page = await listHandler.HandleAsync(
            householdId,
            from,
            to,
            accountId,
            parsedKind,
            search,
            pageSize,
            cursorDate,
            cursorCreatedAt,
            cursorId,
            cancellationToken);

        return Ok(new TransactionPageResponse(
            page.Items.Select(TransactionMapper.From).ToArray(),
            page.HasMore));
    }

    [HttpPut("{transactionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid householdId,
        Guid transactionId,
        UpdateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        await updateHandler.HandleAsync(
            TransactionMapper.ToCommand(householdId, transactionId, request),
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{transactionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Void(
        Guid householdId,
        Guid transactionId,
        [FromQuery] DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken)
    {
        await voidHandler.HandleAsync(
            householdId,
            transactionId,
            expectedUpdatedAt,
            cancellationToken);

        return NoContent();
    }
}
