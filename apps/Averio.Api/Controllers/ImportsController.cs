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
    [HttpPost("ynab")]
    [RequestSizeLimit(YnabRegisterReader.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = YnabRegisterReader.MaxBytes + 65536)]
    public async Task<ActionResult<ImportPreview>> PreviewYnab(Guid householdId, [FromForm] IFormFile file,
        [FromForm] string currency, [FromForm] string dateFormat, [FromForm] string numberFormat,
        CancellationToken ct)
    {
        if (file.Length is <= 0 or > YnabRegisterReader.MaxBytes
            || !(file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                || file.FileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)
                || file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException("import_invalid", "Error_ImportInvalid");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;
        return Ok(await handler.PreviewYnabAsync(householdId, stream, file.FileName,
            currency, dateFormat, numberFormat, ct));
    }

    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult<ImportReview>> Review(Guid householdId, Guid id, ImportReviewRequest request, CancellationToken ct)
        => Ok(await handler.ReviewAsync(householdId, id, request, ct));
    [HttpGet("{id:guid}/balances")]
    public async Task<ActionResult<IReadOnlyList<ImportBalance>>> Balances(Guid householdId, Guid id, CancellationToken ct, [FromQuery] DateOnly? asOf = null)
        => Ok(await handler.BalancesAsync(householdId, id, asOf, ct));
    [HttpGet("{id:guid}/undo-plan")]
    public async Task<ActionResult<ImportUndoPlan>> UndoPlan(Guid householdId, Guid id, CancellationToken ct)
        => Ok(await handler.UndoPlanAsync(householdId, id, ct));
    [HttpPost("{id:guid}/undo")]
    public async Task<ActionResult<ImportUndoResult>> Undo(Guid householdId, Guid id, ImportUndoRequest request, CancellationToken ct)
        => Ok(await handler.UndoAsync(householdId, id, request, ct));
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
