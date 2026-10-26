using System.ComponentModel.DataAnnotations;
using Averio.Application.Receipts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
namespace Averio.Api.Controllers;

[ApiController, Authorize]
[Route("api/v1/households/{householdId:guid}/receipt-scans")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReceiptsController(ReceiptHandler handler) : ControllerBase
{
    [HttpPost, EnableRateLimiting("receipt-uploads")]
    [RequestSizeLimit(ReceiptOptions.MaxUploadBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = ReceiptOptions.MaxUploadBytes + 65536)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ReceiptScanResult>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ReceiptScanResult>> Upload(Guid householdId,
        [FromForm, BindRequired] Guid operationId, [FromForm, Required] IFormFile file, CancellationToken ct)
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
