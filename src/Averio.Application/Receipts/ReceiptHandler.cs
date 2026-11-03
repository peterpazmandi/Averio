using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Receipts;
namespace Averio.Application.Receipts;

public sealed class ReceiptHandler(ICurrentUser user, IHouseholdRepository households,
    IReceiptRepository receipts, IReceiptImageNormalizer images, IReceiptDocumentStore documents,
    IUnitOfWork uow, ReceiptOptions options, TimeProvider clock, ReceiptCategorization? categorization = null, ILogger<ReceiptHandler>? logger = null)
{
    private readonly ILogger<ReceiptHandler> log = logger ?? NullLogger<ReceiptHandler>.Instance;
    public async Task<ReceiptCapabilities> CapabilitiesAsync(Guid household, CancellationToken ct)
    {
        _ = await Authorize(household, ct);
        var now = clock.GetUtcNow();
        var start = ReceiptOptions.MonthStart(now);
        return new(options.Enabled, ReceiptOptions.MaxUploadBytes, options.MonthlyLimit(household, now),
            await receipts.CountMonthlyUploadsAsync(household, start, start.AddMonths(1), ct), start.AddMonths(1),
            options.RetainPostedImages ? options.PostedImageRetentionDays : 0);
    }
    public async Task<ReceiptScanResult> UploadAsync(Guid household, Guid operation, Stream input, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        using var scope = log.BeginScope("ReceiptOperationId={ReceiptOperationId} TraceId={TraceId}", operation, Activity.Current?.TraceId.ToString());
        log.LogInformation("Receipt upload started: OperationId={OperationId}, TraceId={TraceId}, ApiScanningEnabled={ApiScanningEnabled}", operation, Activity.Current?.TraceId.ToString(), options.Enabled);
        try
        {
            var result = await UploadCoreAsync(household, operation, input, ct);
            log.LogInformation("Receipt upload completed: OperationId={OperationId}, ScanId={ScanId}, Status={Status}, ElapsedMs={ElapsedMs}", operation, result.Id, result.Status, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            log.LogWarning("Receipt upload request cancelled: OperationId={OperationId}; admission may already have succeeded, retry the same operation", operation);
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning("Receipt upload failed: OperationId={OperationId}, TraceId={TraceId}, ExceptionType={ExceptionType}, HResult={HResult}, ElapsedMs={ElapsedMs}", operation, Activity.Current?.TraceId.ToString(), ex.GetType().Name, ex.HResult, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
    private async Task<ReceiptScanResult> UploadCoreAsync(Guid household, Guid operation, Stream input, CancellationToken ct)
    {
        _ = await Authorize(household, ct);
        if (operation == Guid.Empty) throw Invalid();
        log.LogDebug("Receipt normalization started: OperationId={OperationId}", operation);
        var image = await images.NormalizeAsync(input, ct);
        log.LogDebug("Receipt normalization completed: OperationId={OperationId}, NormalizedBytes={NormalizedBytes}", operation, image.Length);
        var hash = Convert.ToHexStringLower(SHA256.HashData(image));
        ReceiptScan scan;
        await using (var tx = await uow.BeginTransactionAsync(ct))
        {
            // Existing household lock serializes departure, idempotency and admission limits.
            var member = await Authorize(household, ct);
            var previous = await receipts.FindOperationAsync(household, member, operation, ct);
            if (previous is not null)
            {
                if (previous.ContentHash != hash) throw Conflict();
                log.LogInformation("Receipt upload replay: OperationId={OperationId}, ScanId={ScanId}, Status={Status}; no new scan charged", operation, previous.Id, previous.Status);
                return Map(previous);
            }
            if (!options.Enabled)
            {
                log.LogWarning("Receipt upload rejected: OperationId={OperationId}, Reason=receipt_disabled", operation);
                throw new BusinessRuleException("receipt_disabled", "Error_ReceiptDisabled");
            }
            var now = clock.GetUtcNow();
            var start = ReceiptOptions.MonthStart(now);
            if (await receipts.CountMonthlyUploadsAsync(household, start, start.AddMonths(1), ct) >= options.MonthlyLimit(household, now))
            {
                ReceiptMetrics.Uploads.Add(1, new KeyValuePair<string, object?>("outcome", "monthly_limit"));
                log.LogWarning("Receipt upload rejected: OperationId={OperationId}, Reason=receipt_monthly_limit", operation);
                throw new BusinessRuleException("receipt_monthly_limit", "Error_ReceiptMonthlyLimit");
            }
            if (await receipts.CountUploadsAsync(household, member, now.AddDays(-1), ct) >= options.DailyUploadLimit)
                throw new BusinessRuleException("receipt_limit", "Error_ReceiptLimit");
            scan = ReceiptScan.Create(household, member, operation, hash, image.Length, now, options.RetainPostedImages);
            receipts.Add(scan);
            await uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        ReceiptMetrics.Uploads.Add(1, new KeyValuePair<string, object?>("outcome", "admitted"));
        log.LogInformation("Receipt upload admitted: OperationId={OperationId}, ScanId={ScanId}, Status=Uploading", operation, scan.Id);
        // No database locks across external I/O. Worker cleans abandoned uploads.
        // Anchor the write deadline to the durable record, not to process resumption.
        // Cleanup waits five minutes from creation, including for discarded uploads.
        var remaining = scan.CreatedAt.AddMinutes(1) - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) throw Conflict();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(remaining);
        log.LogInformation("Receipt storage write started: ScanId={ScanId}, Bytes={Bytes}", scan.Id, image.Length);
        try
        {
            await documents.PutAsync(scan.ObjectKey, image, timeout.Token);
            log.LogInformation("Receipt storage write completed: ScanId={ScanId}", scan.Id);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            log.LogWarning("Receipt storage write failed: ScanId={ScanId}, ExceptionType={ExceptionType}, HResult={HResult}", scan.Id, ex.GetType().Name, ex.HResult);
            throw new BusinessRuleException("receipt_storage_unavailable", "Error_ReceiptStorage");
        }
        await using (var tx = await uow.BeginTransactionAsync(ct))
        {
            _ = await Authorize(household, ct);
            if (!await receipts.QueueAsync(scan.Id, clock.GetUtcNow(), ct)) throw Conflict();
            await tx.CommitAsync(ct);
        }
        log.LogInformation("Receipt queued: ScanId={ScanId}, OperationId={OperationId}; a separately running enabled Worker must claim it", scan.Id, operation);
        return await GetAsync(household, scan.Id, ct);
    }
    public async Task<IReadOnlyList<ReceiptScanResult>> ListAsync(Guid household, int skip, CancellationToken ct)
    {
        if (skip < 0 || skip > 10000) throw Invalid();
        var member = await Authorize(household, ct);
        var rows = await receipts.ListAsync(household, member, skip, ct);
        log.LogDebug("Receipt inbox polled: Count={Count}, Skip={Skip}, Queued={Queued}, Processing={Processing}, TraceId={TraceId}", rows.Count, skip, rows.Count(x => x.Status == ReceiptScanStatus.Queued), rows.Count(x => x.Status == ReceiptScanStatus.Processing), Activity.Current?.TraceId.ToString());
        var duplicates = await receipts.DuplicateHashesAsync(household, member, rows.Select(x => x.ContentHash).Distinct().ToArray(), clock.GetUtcNow(), ct);
        var mapped = rows.Select(x => Map(x) with { PossibleDuplicate = duplicates.Contains(x.ContentHash) }).ToArray();
        return categorization is null ? mapped : await categorization.ApplyAsync(household, mapped, ct);
    }
    public async Task<ReceiptScanResult> GetAsync(Guid household, Guid id, CancellationToken ct)
    {
        var scan = await Find(household, id, ct);
        var duplicates = await receipts.DuplicateHashesAsync(household, scan.HouseholdMemberId, [scan.ContentHash], clock.GetUtcNow(), ct);
        var match = duplicates.Contains(scan.ContentHash)
            ? await receipts.FindDuplicateAsync(household, scan.HouseholdMemberId, scan.Id, scan.ContentHash, clock.GetUtcNow(), ct) : null;
        var mapped = Map(scan) with { PossibleDuplicate = match is not null, MatchingReceiptId = match?.Id };
        return categorization is null ? mapped : (await categorization.ApplyAsync(household, [mapped], ct))[0];
    }
    public async Task<byte[]> ImageAsync(Guid household, Guid id, CancellationToken ct)
    {
        var scan = await Find(household, id, ct);
        if (scan.DocumentDeletedAt is not null || scan.Status is ReceiptScanStatus.Discarded or ReceiptScanStatus.Uploading
            || scan.ExpiresAt <= clock.GetUtcNow()) throw new NotFoundException("receipt", id);
        byte[] bytes;
        try { bytes = await documents.GetAsync(scan.ObjectKey, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { throw new BusinessRuleException("receipt_storage_unavailable", "Error_ReceiptStorage"); }
        var current = await Find(household, id, ct);
        if (current.Status == ReceiptScanStatus.Discarded || current.ExpiresAt <= clock.GetUtcNow())
            throw new NotFoundException("receipt", id);
        return bytes;
    }
    public async Task DeleteImageAsync(Guid household, Guid id, Guid version, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var scan = await Find(household, id, ct);
        if (scan.Status != ReceiptScanStatus.Posted) throw Conflict();
        // A retry after expiry/deletion is already satisfied, independent of the old version.
        if (scan.DocumentDeletedAt is not null || scan.ExpiresAt <= clock.GetUtcNow()) return;
        if (!await receipts.DeletePostedImageAsync(scan.Id, version, clock.GetUtcNow(), ct)) throw Conflict();
        await tx.CommitAsync(ct);
    }
    public async Task DiscardAsync(Guid household, Guid id, Guid version, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var scan = await Find(household, id, ct);
        if (!await receipts.DiscardAsync(scan.Id, version, clock.GetUtcNow(), ct)) throw Conflict();
        await tx.CommitAsync(ct);
    }
    private async Task<ReceiptScan> Find(Guid household, Guid id, CancellationToken ct)
    {
        var member = await Authorize(household, ct);
        return await receipts.GetAsync(household, member, id, ct) ?? throw new NotFoundException("receipt", id);
    }
    private async Task<Guid> Authorize(Guid household, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new AuthenticationFailedException();
        var h = await households.GetByIdForUserAsync(household, userId, ct) ?? throw new NotFoundException("household", household);
        return h.Members.Single(x => x.UserId == userId && x.LeftAtUtc == null).Id;
    }
    private ReceiptScanResult Map(ReceiptScan scan)
    {
        var expired = scan.ExpiresAt <= clock.GetUtcNow();
        return new(scan.Id, expired && scan.Status != ReceiptScanStatus.Posted ? "Expired" : scan.Status.ToString(), scan.Version, scan.Attempts,
            scan.CreatedAt, scan.ExpiresAt, scan.FailureCode,
            !expired && scan.Status == ReceiptScanStatus.Ready && scan.DraftJson is not null
                ? JsonSerializer.Deserialize<ReceiptDraft>(scan.DraftJson) : null, scan.PostedTransactionId,
            !expired && scan.DocumentDeletedAt is null && scan.Status is not (ReceiptScanStatus.Discarded or ReceiptScanStatus.Uploading));
    }
    private static BusinessRuleException Invalid() => new("receipt_invalid", "Error_ReceiptInvalid");
    private static ConflictException Conflict() => new("receipt_changed", "Error_ReceiptChanged");
}


