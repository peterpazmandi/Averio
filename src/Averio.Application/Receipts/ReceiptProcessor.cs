using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace Averio.Application.Receipts;

public sealed class ReceiptProcessor(IReceiptRepository receipts, IReceiptDocumentStore documents,
    IReceiptExtractor extractor, ReceiptOptions options, TimeProvider clock, ReceiptCategorization? categorization = null, ILogger<ReceiptProcessor>? logger = null)
{
    private readonly ILogger<ReceiptProcessor> log = logger ?? NullLogger<ReceiptProcessor>.Instance;
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (!options.Enabled) return false;
        var scan = await receipts.ClaimAsync(clock.GetUtcNow(), ct);
        if (scan is null) { log.LogDebug("Receipt queue has no eligible scan"); return false; }
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        ReceiptMetrics.QueueAgeSeconds.Record(Math.Max(0, (clock.GetUtcNow() - scan.CreatedAt).TotalSeconds));
        using var scope = log.BeginScope("ReceiptScanId={ReceiptScanId} OperationId={OperationId} Attempt={Attempt}", scan.Id, scan.OperationId, scan.Attempts);
        log.LogInformation("Receipt claimed: ScanId={ScanId}, OperationId={OperationId}, Attempt={Attempt}, QueueAgeSeconds={QueueAgeSeconds}, LeaseUntil={LeaseUntil}", scan.Id, scan.OperationId, scan.Attempts, Math.Max(0, (clock.GetUtcNow() - scan.CreatedAt).TotalSeconds), scan.LeaseUntil);
        var stage = "storage_read";
        string? draft = null, failure = null;
        var transient = false;
        using var timeout = ReceiptTimeouts.CreateProcessingCancellation(ct);
        try
        {
            log.LogInformation("Receipt stage started: ScanId={ScanId}, Stage={Stage}", scan.Id, stage);
            var bytes = await documents.GetAsync(scan.ObjectKey, timeout.Token);
            log.LogInformation("Receipt storage read completed: ScanId={ScanId}, Bytes={Bytes}", scan.Id, bytes.Length);
            stage = "category_catalog";
            log.LogDebug("Receipt stage started: ScanId={ScanId}, Stage={Stage}", scan.Id, stage);
            IReadOnlyList<ReceiptCategoryOption> catalog = categorization is null ? []
                : (await categorization.CatalogAsync(scan.HouseholdId, timeout.Token)).Take(500).ToArray();
            stage = "provider";
            log.LogInformation("Receipt stage started: ScanId={ScanId}, Stage={Stage}, HttpTimeoutSeconds={HttpTimeoutSeconds}, ProcessingTimeoutSeconds={ProcessingTimeoutSeconds}", scan.Id, stage, ReceiptTimeouts.ProviderRequest.TotalSeconds, ReceiptTimeouts.Processing.TotalSeconds);
            var extracted = await extractor.ExtractAsync(bytes, catalog, timeout.Token);
            stage = "validation";
            var result = ReceiptDraftValidation.Validate(extracted);
            var allowed = catalog.Select(c => c.Id).ToHashSet();
            result = result with { Lines = result.Lines.Select(line => line with {
                SuggestedCategoryId = line.Kind == "Item" && line.SuggestedCategoryId is Guid id && allowed.Contains(id) ? id : null,
                CategorySuggestionSource = null
            }).ToArray() };
            draft = JsonSerializer.Serialize(result);
            if (draft.Length > 262144) throw new ReceiptExtractionException("invalid_extraction", false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { log.LogInformation("Receipt processing interrupted by shutdown/cancellation: ScanId={ScanId}, Stage={Stage}; lease recovery will handle unfinished work", scan.Id, stage); throw; }
        catch (OperationCanceledException) { failure = "timeout"; transient = true; }
        catch (ReceiptExtractionException ex) { failure = ex.Code; transient = ex.Transient; }
        catch (Exception ex) { failure = "processing_failed"; transient = true; log.LogWarning("Receipt processing exception: ScanId={ScanId}, Stage={Stage}, ExceptionType={ExceptionType}, HResult={HResult}", scan.Id, stage, ex.GetType().Name, ex.HResult); }
        if (failure is not null)
        {
            draft = null;
            log.LogWarning("Receipt extraction failed: ScanId={ScanId}, Stage={Stage}, FailureCode={FailureCode}, Transient={Transient}, Attempt={Attempt}", scan.Id, stage, failure, transient, scan.Attempts);
        }
        log.LogDebug("Receipt result persistence started: ScanId={ScanId}", scan.Id);
        bool accepted;
        var finishedAt = clock.GetUtcNow();
        try { accepted = await receipts.FinishAsync(scan.Id, scan.LeaseToken!.Value, finishedAt, draft, failure, transient, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            log.LogError("Receipt result persistence failed: ScanId={ScanId}, ExceptionType={ExceptionType}, HResult={HResult}; unfinished work remains subject to lease recovery", scan.Id, ex.GetType().Name, ex.HResult);
            throw;
        }
        var outcome = !accepted ? "fenced" : draft is not null ? "ready" : transient && scan.Attempts < ReceiptOptions.MaxAttempts ? "retry" : "failed";
        ReceiptMetrics.Extractions.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        ReceiptMetrics.ProcessingSeconds.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds,
            new KeyValuePair<string, object?>("outcome", outcome));
        log.LogInformation("Receipt processing completed: ScanId={ScanId}, Outcome={Outcome}, Attempt={Attempt}, ElapsedMs={ElapsedMs}, NextAttemptAt={NextAttemptAt}", scan.Id, outcome, scan.Attempts, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, outcome == "retry" ? finishedAt.AddMinutes(1) : (DateTimeOffset?)null);
        if (!accepted) log.LogWarning("Receipt result rejected by lease/status fence: ScanId={ScanId}", scan.Id);
        return true;
    }
    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        var failures = 0;
        foreach (var scan in await receipts.CleanupCandidatesAsync(clock.GetUtcNow(), ct))
        {
            // Expire atomically reserves this cleanup attempt for two minutes.
            if (!await receipts.ExpireAsync(scan.Id, clock.GetUtcNow(), ct)) continue;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                log.LogInformation("Receipt cleanup started: ScanId={ScanId}", scan.Id);
                await documents.DeleteAsync(scan.ObjectKey, timeout.Token);
                await receipts.MarkDocumentDeletedAsync(scan.Id, clock.GetUtcNow(), ct);
                ReceiptMetrics.Cleanup.Add(1, new KeyValuePair<string, object?>("outcome", "deleted"));
                log.LogInformation("Receipt cleanup completed: ScanId={ScanId}", scan.Id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                failures++;
                log.LogWarning("Receipt cleanup failed: ScanId={ScanId}, ExceptionType={ExceptionType}, HResult={HResult}; retry deferred one minute", scan.Id, ex.GetType().Name, ex.HResult);
                ReceiptMetrics.Cleanup.Add(1, new KeyValuePair<string, object?>("outcome", "retry"));
                // A poisoned object must not monopolize the oldest-first batch.
                await receipts.DeferCleanupAsync(scan.Id, clock.GetUtcNow().AddMinutes(1), ct);
            }
        }
        return failures;
    }
}
