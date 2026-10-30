using System.Security.Cryptography;
using System.Text.Json;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Receipts;
namespace Averio.Application.Receipts;

public sealed class ReceiptHandler(ICurrentUser user, IHouseholdRepository households,
    IReceiptRepository receipts, IReceiptImageNormalizer images, IReceiptDocumentStore documents,
    IUnitOfWork uow, ReceiptOptions options, TimeProvider clock, ReceiptCategorization? categorization = null)
{
    public async Task<ReceiptCapabilities> CapabilitiesAsync(Guid household, CancellationToken ct)
    {
        _ = await Authorize(household, ct);
        return new(options.Enabled, ReceiptOptions.MaxUploadBytes);
    }
    public async Task<ReceiptScanResult> UploadAsync(Guid household, Guid operation, Stream input, CancellationToken ct)
    {
        if (!options.Enabled) throw new BusinessRuleException("receipt_disabled", "Error_ReceiptDisabled");
        _ = await Authorize(household, ct);
        if (operation == Guid.Empty) throw Invalid();
        var image = await images.NormalizeAsync(input, ct);
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
                return Map(previous);
            }
            var now = clock.GetUtcNow();
            if (await receipts.CountUploadsAsync(household, member, now.AddDays(-1), ct) >= options.DailyUploadLimit)
                throw new BusinessRuleException("receipt_limit", "Error_ReceiptLimit");
            scan = ReceiptScan.Create(household, member, operation, hash, image.Length, now);
            receipts.Add(scan);
            await uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        // No database locks across external I/O. Worker cleans abandoned uploads.
        // Anchor the write deadline to the durable record, not to process resumption.
        // Cleanup waits five minutes from creation, including for discarded uploads.
        var remaining = scan.CreatedAt.AddMinutes(1) - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) throw Conflict();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(remaining);
        try { await documents.PutAsync(scan.ObjectKey, image, timeout.Token); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { throw new BusinessRuleException("receipt_storage_unavailable", "Error_ReceiptStorage"); }
        await using (var tx = await uow.BeginTransactionAsync(ct))
        {
            _ = await Authorize(household, ct);
            if (!await receipts.QueueAsync(scan.Id, clock.GetUtcNow(), ct)) throw Conflict();
            await tx.CommitAsync(ct);
        }
        return await GetAsync(household, scan.Id, ct);
    }
    public async Task<IReadOnlyList<ReceiptScanResult>> ListAsync(Guid household, int skip, CancellationToken ct)
    {
        if (skip < 0 || skip > 10000) throw Invalid();
        var member = await Authorize(household, ct);
        var mapped = (await receipts.ListAsync(household, member, skip, ct)).Select(Map).ToArray();
        return categorization is null ? mapped : await categorization.ApplyAsync(household, mapped, ct);
    }
    public async Task<ReceiptScanResult> GetAsync(Guid household, Guid id, CancellationToken ct)
    {
        var mapped = Map(await Find(household, id, ct));
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
                ? JsonSerializer.Deserialize<ReceiptDraft>(scan.DraftJson) : null, scan.PostedTransactionId);
    }
    private static BusinessRuleException Invalid() => new("receipt_invalid", "Error_ReceiptInvalid");
    private static ConflictException Conflict() => new("receipt_changed", "Error_ReceiptChanged");
}

