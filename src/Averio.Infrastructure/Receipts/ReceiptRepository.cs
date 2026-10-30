using Averio.Application.Receipts;
using Averio.Domain.Receipts;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Averio.Infrastructure.Receipts;

public sealed class ReceiptRepository(AverioDbContext db) : IReceiptRepository
{
    private IQueryable<ReceiptScan> Owned(Guid h, Guid m) => db.Set<ReceiptScan>().AsNoTracking()
        .Where(x => x.HouseholdId == h && x.HouseholdMemberId == m
            && db.HouseholdMembers.Any(y => y.HouseholdId == h && y.Id == m && y.LeftAtUtc == null));
    public Task<ReceiptScan?> FindOperationAsync(Guid h, Guid m, Guid op, CancellationToken ct)
        => Owned(h,m).SingleOrDefaultAsync(x => x.OperationId == op, ct);
    public Task<int> CountUploadsAsync(Guid h, Guid m, DateTimeOffset since, CancellationToken ct)
        => Owned(h,m).CountAsync(x => x.CreatedAt >= since, ct);
    public void Add(ReceiptScan scan) => db.Add(scan);
    public async Task<bool> QueueAsync(Guid id, DateTimeOffset now, CancellationToken ct)
        => await db.Set<ReceiptScan>().Where(x => x.Id == id && x.Status == ReceiptScanStatus.Uploading
            && x.CreatedAt > now.AddMinutes(-5) && x.DocumentDeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ReceiptScanStatus.Queued)
                .SetProperty(x => x.UpdatedAt, now).SetProperty(x => x.Version, Guid.NewGuid()), ct) == 1;
    public async Task<IReadOnlyList<ReceiptScan>> ListAsync(Guid h, Guid m, int skip, CancellationToken ct)
        => await Owned(h,m).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(skip).Take(50).ToListAsync(ct);
    public Task<ReceiptScan?> GetAsync(Guid h, Guid m, Guid id, CancellationToken ct)
        => Owned(h,m).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<bool> DiscardAsync(Guid id, Guid version, DateTimeOffset now, CancellationToken ct)
        => await db.Set<ReceiptScan>().Where(x => x.Id == id && x.Version == version && x.Status != ReceiptScanStatus.Posted)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ReceiptScanStatus.Discarded)
                .SetProperty(x => x.DraftJson, (string?)null).SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.Version, Guid.NewGuid()).SetProperty(x => x.UpdatedAt, now), ct) == 1;
    public async Task<bool> PostAsync(Guid id, Guid version, Guid transactionId, string confirmationFingerprint, DateTimeOffset now, CancellationToken ct)
        => await db.Set<ReceiptScan>().Where(x => x.Id == id && x.Version == version
                && x.Status == ReceiptScanStatus.Ready && x.ExpiresAt > now && x.DocumentDeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ReceiptScanStatus.Posted)
                .SetProperty(x => x.PostedTransactionId, transactionId).SetProperty(x => x.ConfirmationFingerprint, confirmationFingerprint)
                .SetProperty(x => x.DraftJson, (string?)null)
                .SetProperty(x => x.Version, Guid.NewGuid()).SetProperty(x => x.UpdatedAt, now), ct) == 1;
    public async Task<ReceiptScan?> ClaimAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Final-attempt crashes must not leave a job permanently Processing.
        await db.Set<ReceiptScan>().Where(x => x.Status == ReceiptScanStatus.Processing
                && x.LeaseUntil <= now && x.Attempts >= ReceiptOptions.MaxAttempts)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ReceiptScanStatus.Failed)
                .SetProperty(x => x.FailureCode, "attempts_exhausted").SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAt, now).SetProperty(x => x.Version, Guid.NewGuid()), ct);
        var token = Guid.NewGuid();
        var lease = now.AddMinutes(2);
        // One atomic claim, no transaction held during provider I/O.
        var result = await db.Set<ReceiptScan>().FromSqlInterpolated($"""
            UPDATE receipt_scans AS r SET status = 'Processing', attempts = attempts + 1,
                lease_token = {token}, lease_until = {lease}, updated_at = {now}, version = {token}
            WHERE r.id = (
                SELECT s.id FROM receipt_scans s
                WHERE ((s.status = 'Queued' AND s.next_attempt_at <= {now})
                    OR (s.status = 'Processing' AND s.lease_until <= {now}))
                    AND s.attempts < 3 AND s.expires_at > {now}
                    AND EXISTS (SELECT 1 FROM household_members m JOIN households h ON h.id = m.household_id
                        WHERE m.id = s.household_member_id AND m.household_id = s.household_id
                        AND m.left_at_utc IS NULL AND h.archived_at_utc IS NULL)
                ORDER BY s.next_attempt_at, s.id FOR UPDATE OF s SKIP LOCKED LIMIT 1
            ) RETURNING r.*
            """).AsNoTracking().ToListAsync(ct);
        return result.SingleOrDefault();
    }
    public async Task<bool> FinishAsync(Guid id, Guid token, DateTimeOffset now, string? draft,
        string? failure, bool transient, CancellationToken ct)
        => await db.Set<ReceiptScan>().Where(x => x.Id == id && x.Status == ReceiptScanStatus.Processing
                && x.LeaseToken == token && x.LeaseUntil > now && x.ExpiresAt > now
                && db.HouseholdMembers.Any(m => m.Id == x.HouseholdMemberId && m.HouseholdId == x.HouseholdId && m.LeftAtUtc == null))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, x => draft != null ? ReceiptScanStatus.Ready
                    : transient && x.Attempts < ReceiptOptions.MaxAttempts ? ReceiptScanStatus.Queued : ReceiptScanStatus.Failed)
                .SetProperty(x => x.DraftJson, draft).SetProperty(x => x.FailureCode, failure)
                .SetProperty(x => x.NextAttemptAt, now.AddMinutes(1))
                .SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.Version, Guid.NewGuid()).SetProperty(x => x.UpdatedAt, now), ct) == 1;
    private IQueryable<ReceiptScan> Expired(DateTimeOffset now) => db.Set<ReceiptScan>().Where(x => x.DocumentDeletedAt == null
        // Even a discarded/departed upload may still be writing its object.
        // Its one-minute write deadline is anchored to CreatedAt; allow a settling
        // interval before recording physical deletion, including after API crashes.
        && x.CreatedAt <= now.AddMinutes(-5) && x.NextAttemptAt <= now &&
        (x.Status == ReceiptScanStatus.Discarded || x.ExpiresAt <= now
            || (x.Status == ReceiptScanStatus.Uploading && x.CreatedAt <= now.AddMinutes(-5))
            || !db.HouseholdMembers.Any(m => m.Id == x.HouseholdMemberId && m.HouseholdId == x.HouseholdId && m.LeftAtUtc == null)));
    public async Task<IReadOnlyList<ReceiptScan>> CleanupCandidatesAsync(DateTimeOffset now, CancellationToken ct)
        => await Expired(now).AsNoTracking().OrderBy(x => x.NextAttemptAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(50).ToListAsync(ct);
    public async Task<bool> ExpireAsync(Guid id, DateTimeOffset now, CancellationToken ct)
        => await Expired(now).Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, x => x.Status == ReceiptScanStatus.Posted ? ReceiptScanStatus.Posted : ReceiptScanStatus.Discarded)
                .SetProperty(x => x.NextAttemptAt, now.AddMinutes(2))
                .SetProperty(x => x.DraftJson, (string?)null).SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.Version, Guid.NewGuid()).SetProperty(x => x.UpdatedAt, now), ct) == 1;
    public async Task DeferCleanupAsync(Guid id, DateTimeOffset retryAt, CancellationToken ct)
        => await db.Set<ReceiptScan>().Where(x => x.Id == id && (x.Status == ReceiptScanStatus.Discarded || x.Status == ReceiptScanStatus.Posted)
                && x.DocumentDeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, retryAt), ct);
    public async Task MarkDocumentDeletedAsync(Guid id, DateTimeOffset now, CancellationToken ct)
        => await db.Set<ReceiptScan>().Where(x => x.Id == id && (x.Status == ReceiptScanStatus.Discarded || x.Status == ReceiptScanStatus.Posted))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DocumentDeletedAt, now), ct);
}
