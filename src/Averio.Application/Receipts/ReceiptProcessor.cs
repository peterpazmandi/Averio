using System.Text.Json;
namespace Averio.Application.Receipts;

public sealed class ReceiptProcessor(IReceiptRepository receipts, IReceiptDocumentStore documents,
    IReceiptExtractor extractor, ReceiptOptions options, TimeProvider clock)
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
            var result = await extractor.ExtractAsync(bytes, timeout.Token);
            draft = JsonSerializer.Serialize(ReceiptDraftValidation.Validate(result));
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
    public async Task CleanupAsync(CancellationToken ct)
    {
        foreach (var scan in await receipts.CleanupCandidatesAsync(clock.GetUtcNow(), ct))
        {
            if (!await receipts.ExpireAsync(scan.Id, clock.GetUtcNow(), ct)) continue;
            await documents.DeleteAsync(scan.ObjectKey, ct);
            await receipts.MarkDocumentDeletedAsync(scan.Id, clock.GetUtcNow(), ct);
        }
    }
}
