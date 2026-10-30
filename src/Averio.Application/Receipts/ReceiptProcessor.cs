using System.Text.Json;
namespace Averio.Application.Receipts;

public sealed class ReceiptProcessor(IReceiptRepository receipts, IReceiptDocumentStore documents,
    IReceiptExtractor extractor, ReceiptOptions options, TimeProvider clock, ReceiptCategorization? categorization = null)
{
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (!options.Enabled) return false;
        var scan = await receipts.ClaimAsync(clock.GetUtcNow(), ct);
        if (scan is null) return false;
        string? draft = null, failure = null;
        var transient = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            var bytes = await documents.GetAsync(scan.ObjectKey, timeout.Token);
            IReadOnlyList<ReceiptCategoryOption> catalog = categorization is null ? []
                : (await categorization.CatalogAsync(scan.HouseholdId, timeout.Token)).Take(500).ToArray();
            var result = ReceiptDraftValidation.Validate(await extractor.ExtractAsync(bytes, catalog, timeout.Token));
            var allowed = catalog.Select(c => c.Id).ToHashSet();
            result = result with { Lines = result.Lines.Select(line => line with {
                SuggestedCategoryId = line.Kind == "Item" && line.SuggestedCategoryId is Guid id && allowed.Contains(id) ? id : null,
                CategorySuggestionSource = null
            }).ToArray() };
            draft = JsonSerializer.Serialize(result);
            if (draft.Length > 262144) throw new ReceiptExtractionException("invalid_extraction", false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { failure = "timeout"; transient = true; }
        catch (ReceiptExtractionException ex) { failure = ex.Code; transient = ex.Transient; }
        catch (Exception) { failure = "processing_failed"; transient = true; }
        if (failure is not null) draft = null;
        await receipts.FinishAsync(scan.Id, scan.LeaseToken!.Value, clock.GetUtcNow(), draft, failure, transient, ct);
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
                await documents.DeleteAsync(scan.ObjectKey, timeout.Token);
                await receipts.MarkDocumentDeletedAsync(scan.Id, clock.GetUtcNow(), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                failures++;
                // A poisoned object must not monopolize the oldest-first batch.
                await receipts.DeferCleanupAsync(scan.Id, clock.GetUtcNow().AddMinutes(1), ct);
            }
        }
        return failures;
    }
}
