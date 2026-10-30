using System.ComponentModel.DataAnnotations;
using Averio.Application.Receipts;
using Averio.Api.Mapping;
using Averio.Contracts.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
namespace Averio.Api.Controllers;

[ApiController, Authorize]
[Route("api/v1/households/{householdId:guid}/receipt-scans")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReceiptsController(ReceiptHandler handler, PostReceiptHandler posting) : ControllerBase
{
    [HttpGet("capabilities")]
    public Task<ReceiptCapabilities> Capabilities(Guid householdId, CancellationToken ct)
        => handler.CapabilitiesAsync(householdId, ct);

    [HttpPost("{id:guid}/transaction")]
    public async Task<CreateTransactionResponse> Post(Guid householdId, Guid id, PostReceiptRequest request, CancellationToken ct)
    {
        if (request.RememberCategoryRules?.Any(r => r is null) == true)
            throw new Averio.Application.Common.Exceptions.BusinessRuleException("receipt_category_rule_invalid", "Error_ReceiptCategoryRuleInvalid");
        var command = TransactionMapper.ToCommand(householdId, request.Transaction);
        return new(await posting.HandleAsync(householdId, id, request.ExpectedVersion,
            request.Currency, DecimalAmountMapper.ParseTransactionAmount(request.ReviewedTotal), command, ct,
            request.RememberCategoryRules?.Select(r => new RememberReceiptCategory(r.LineIndex, r.ItemIndex)).ToArray()));
    }
    [HttpPost, EnableRateLimiting("receipt-uploads")]
    [RequestSizeLimit(ReceiptOptions.MaxUploadBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = ReceiptOptions.MaxUploadBytes + 65536)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ReceiptScanResult>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ReceiptScanResult>> Upload(Guid householdId,
        [FromForm, BindRequired] Guid operationId, [Required] IFormFile file, CancellationToken ct)
    {
        await using var input = file.OpenReadStream();
        var result = await handler.UploadAsync(householdId, operationId, input, ct);
        return AcceptedAtAction(nameof(Get), new { householdId, id = result.Id }, result);
    }
    [HttpGet]
    public Task<IReadOnlyList<ReceiptScanResult>> List(Guid householdId, CancellationToken ct, [FromQuery] int skip = 0)
        => handler.ListAsync(householdId, skip, ct);
    [HttpGet("{id:guid}")]
    public Task<ReceiptScanResult> Get(Guid householdId, Guid id, CancellationToken ct) => handler.GetAsync(householdId, id, ct);
    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "image/jpeg")]
    public async Task<IActionResult> Image(Guid householdId, Guid id, CancellationToken ct)
    {
        var bytes = await handler.ImageAsync(householdId, id, ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(bytes, "image/jpeg");
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Discard(Guid householdId, Guid id, [FromQuery, BindRequired] Guid expectedVersion, CancellationToken ct)
    {
        await handler.DiscardAsync(householdId, id, expectedVersion, ct);
        return NoContent();
    }
}

