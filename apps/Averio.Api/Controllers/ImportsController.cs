using Averio.Application.Imports;
using Averio.Application.Common.Exceptions;
using Averio.Infrastructure.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/imports")]
public sealed class ImportsController(ImportHandler handler) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(MoneyManagerXlsxReader.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = MoneyManagerXlsxReader.MaxBytes + 65536)]
    public async Task<ActionResult<ImportPreview>> Preview(Guid householdId, IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > MoneyManagerXlsxReader.MaxBytes || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("import_invalid", "Error_ImportInvalid");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct); stream.Position = 0;
        return Ok(await handler.PreviewAsync(householdId, stream, ct, file.FileName));
    }
    [HttpGet]
    public async Task<ActionResult<ImportHistoryPage>> History(Guid householdId, CancellationToken ct, [FromQuery] int page = 0)
        => Ok(await handler.HistoryAsync(householdId, page, ct));
    [HttpGet("{id:guid}/records")]
    public async Task<ActionResult<ImportHistoryDetail>> Records(Guid householdId, Guid id, CancellationToken ct, [FromQuery] int page = 0)
        => Ok(await handler.HistoryDetailAsync(householdId, id, page, ct));
    [HttpGet("{id:guid}/undo-preview")]
    public async Task<ActionResult<ImportHistoryDetail>> UndoPreview(Guid householdId, Guid id, CancellationToken ct, [FromQuery] int page = 0)
        => Ok(await handler.HistoryDetailAsync(householdId, id, page, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ImportPreview>> Get(Guid householdId, Guid id, CancellationToken ct)
        => Ok(await handler.GetAsync(householdId, id, ct));
    [HttpPost("{id:guid}/confirmation")]
    public async Task<ActionResult<ImportResult>> Confirm(Guid householdId, Guid id, ConfirmImportRequest request, CancellationToken ct)
        => Ok(await handler.ConfirmAsync(householdId, id, request, ct));
}

