using System.Collections.Concurrent;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Receipts;
using Averio.Domain.Receipts;
using Averio.Infrastructure.Persistence;
using Averio.Infrastructure.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

internal static class ReceiptVerification
{
    public static async Task RunAsync(ServiceProvider provider, MutableCurrentUser user, Fixture f, DateTimeOffset now)
    {
        var clock = new MutableTimeProvider(now);
        var options = new ReceiptOptions { Enabled = true, DailyUploadLimit = 20 };
        var store = new Documents();
        var extract = new Extractor();
        using var bitmap = new SKBitmap(64, 48); bitmap.Erase(SKColors.White);
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var image = png.ToArray();
        ReceiptHandler Handler(IServiceProvider s) => new(user, s.GetRequiredService<IHouseholdRepository>(),
            s.GetRequiredService<IReceiptRepository>(), new ReceiptImageNormalizer(), store, s.GetRequiredService<IUnitOfWork>(), options, clock);
        ReceiptProcessor Processor(IServiceProvider s) => new(s.GetRequiredService<IReceiptRepository>(), store, extract, options, clock);
        async Task<ReceiptScanResult> Upload(Guid operation)
        { using var s = provider.CreateScope(); return await Handler(s.ServiceProvider).UploadAsync(f.HouseholdId, operation, new MemoryStream(image), default); }
        async Task<ReceiptScanResult> Get(Guid id)
        { using var s = provider.CreateScope(); return await Handler(s.ServiceProvider).GetAsync(f.HouseholdId, id, default); }
        async Task Discard(ReceiptScanResult scan)
        { using var s = provider.CreateScope(); await Handler(s.ServiceProvider).DiscardAsync(f.HouseholdId, scan.Id, scan.Version, default); }
        async Task<ReceiptScan?> Claim(DateTimeOffset at)
        { using var s = provider.CreateScope(); return await s.ServiceProvider.GetRequiredService<IReceiptRepository>().ClaimAsync(at, default); }
        async Task<bool> Finish(ReceiptScan scan, DateTimeOffset at, bool transient)
        { using var s = provider.CreateScope(); return await s.ServiceProvider.GetRequiredService<IReceiptRepository>().FinishAsync(scan.Id, scan.LeaseToken!.Value, at, null, "timeout", transient, default); }
        int before;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            before = await db.Transactions.CountAsync();
            Check(!db.Database.HasPendingModelChanges(), "receipt migration matches model");
        }
        var op = Guid.NewGuid();
        var first = await Upload(op);
        Check((await Upload(op)).Id == first.Id && store.Puts == 1, "idempotent upload");
        var raceOp = Guid.NewGuid();
        var race = await Task.WhenAll(Upload(raceOp), Upload(raceOp));
        Check(race[0].Id == race[1].Id && store.Puts == 2, "concurrent idempotent admission");
        await Discard(await Get(race[0].Id));
        user.UserId = f.MemberUserId;
        await Reject<NotFoundException>(() => Get(first.Id));
        using (var scope = provider.CreateScope())
        {
            var handler = Handler(scope.ServiceProvider);
            Check((await handler.ListAsync(f.HouseholdId, 0, default)).Count == 0, "private list");
            await Reject<NotFoundException>(() => handler.ImageAsync(f.HouseholdId, first.Id, default));
        }
        user.UserId = f.OwnerUserId;
        using (var scope = provider.CreateScope())
            await Reject<NotFoundException>(() => Handler(scope.ServiceProvider).GetAsync(Guid.NewGuid(), first.Id, default));
        var claims = await Task.WhenAll(Claim(now), Claim(now));
        Check(claims.Count(x => x != null) == 1, "one concurrent claimant");
        var initial = claims.Single(x => x != null)!;
        var reclaimed = (await Claim(now.AddMinutes(3)))!;
        Check(reclaimed.Id == first.Id && reclaimed.Attempts == 2, "expired lease reclaimed");
        Check(!await Finish(initial, now.AddMinutes(3), true), "old lease fenced");
        Check(await Finish(reclaimed, now.AddMinutes(3), true), "retry queued");
        Check(await Claim(now.AddMinutes(3)) == null, "backoff respected");
        Check((await Claim(now.AddMinutes(5)))!.Attempts == 3, "retry budget");
        Check(await Claim(now.AddMinutes(8)) == null && (await Get(first.Id)).Status == "Failed", "final crash terminal");
        var ready = await Upload(Guid.NewGuid());
        using (var scope = provider.CreateScope()) Check(await Processor(scope.ServiceProvider).ProcessNextAsync(default), "worker processed");
        var result = await Get(ready.Id);
        Check(result.Status == "Ready" && result.Draft!.Total == "9.00" && result.Draft.Lines[1].Amount == "-1.00", "ready draft");
        using (var scope = provider.CreateScope()) Check((await Handler(scope.ServiceProvider).ImageAsync(f.HouseholdId, ready.Id, default)).Length > 0, "authorized image");
        await Reject<ConflictException>(() => Discard(ready));
        await Discard(result);
        Check((await Get(ready.Id)).Draft == null, "discard clears draft");
        using (var scope = provider.CreateScope()) await Reject<NotFoundException>(() => Handler(scope.ServiceProvider).ImageAsync(f.HouseholdId, ready.Id, default));
        var inflight = await Upload(Guid.NewGuid());
        var lease = (await Claim(now))!;
        await Discard(await Get(inflight.Id));
        Check(!await Finish(lease, now, false), "discard fences provider completion");
        extract.Fail = true;
        var invalid = await Upload(Guid.NewGuid());
        using (var scope = provider.CreateScope()) await Processor(scope.ServiceProvider).ProcessNextAsync(default);
        Check((await Get(invalid.Id)).Status == "Failed" && (await Get(invalid.Id)).Attempts == 1, "permanent provider error");
        extract.Fail = false;
        options.DailyUploadLimit = 1;
        await Reject<BusinessRuleException>(() => Upload(Guid.NewGuid()));
        Check((await Upload(op)).Id == first.Id, "retry bypasses admission quota");
        options.Enabled = false;
        await Reject<BusinessRuleException>(() => Upload(Guid.NewGuid()));
        using (var scope = provider.CreateScope()) Check(!await Processor(scope.ServiceProvider).ProcessNextAsync(default), "disabled worker");
        options.Enabled = true; options.DailyUploadLimit = 20;

        // Discard while the object write is still in flight. No cleanup completion
        // may be recorded until the durable upload settling interval has elapsed.
        store.PutStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PutRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedOperation = Guid.NewGuid();
        var pendingUpload = Upload(delayedOperation);
        await store.PutStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        ReceiptScan delayed;
        using (var scope = provider.CreateScope())
            delayed = (await scope.ServiceProvider.GetRequiredService<IReceiptRepository>()
                .FindOperationAsync(f.HouseholdId, f.OwnerMemberId, delayedOperation, default))!;
        await Discard(await Get(delayed.Id));
        using (var scope = provider.CreateScope())
        {
            await Processor(scope.ServiceProvider).CleanupAsync(default);
            var row = await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Set<ReceiptScan>().AsNoTracking().SingleAsync(x => x.Id == delayed.Id);
            Check(row.DocumentDeletedAt == null, "discard must not finalize deletion during upload");
        }
        store.PutRelease.SetResult();
        await Reject<ConflictException>(() => pendingUpload);
        store.PutStarted = null; store.PutRelease = null;
        Check(store.Files.ContainsKey(delayed.ObjectKey), "late upload completed after discard");
        clock.Advance(TimeSpan.FromMinutes(5));
        using (var scope = provider.CreateScope())
            await Processor(scope.ServiceProvider).CleanupAsync(default);
        Check(!store.Files.ContainsKey(delayed.ObjectKey), "late object is eventually deleted");

        // Simulate an API crash after the storage write: no QueueAsync or handler
        // completion/finally runs. Cleanup must recover using persisted state only.
        var abandoned = ReceiptScan.Create(f.HouseholdId, f.OwnerMemberId, Guid.NewGuid(), new string('b', 64), image.Length, clock.GetUtcNow());
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            db.Add(abandoned); await db.SaveChangesAsync();
        }
        await store.PutAsync(abandoned.ObjectKey, image, default);
        await Discard(await Get(abandoned.Id));
        using (var scope = provider.CreateScope())
            await Processor(scope.ServiceProvider).CleanupAsync(default);
        Check(store.Files.ContainsKey(abandoned.ObjectKey), "crashed upload respects settling interval");
        clock.Advance(TimeSpan.FromMinutes(5));
        using (var scope = provider.CreateScope())
            await Processor(scope.ServiceProvider).CleanupAsync(default);
        Check(!store.Files.ContainsKey(abandoned.ObjectKey), "crashed upload is durably cleaned");

        // A permanently failing object must not block another deletion or an
        // extraction. Retry scheduling also keeps it out of the next poll.
        var poisoned = await Upload(Guid.NewGuid());
        var healthy = await Upload(Guid.NewGuid());
        await Discard(await Get(poisoned.Id));
        await Discard(await Get(healthy.Id));
        string poisonKey, healthyKey;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            poisonKey = (await db.Set<ReceiptScan>().AsNoTracking().SingleAsync(x => x.Id == poisoned.Id)).ObjectKey;
            healthyKey = (await db.Set<ReceiptScan>().AsNoTracking().SingleAsync(x => x.Id == healthy.Id)).ObjectKey;
        }
        clock.Advance(TimeSpan.FromMinutes(5));
        store.FailDelete = poisonKey;
        var next = await Upload(Guid.NewGuid());
        extract.Date = "10/11/26";
        using (var scope = provider.CreateScope())
        {
            Check(await Processor(scope.ServiceProvider).CleanupAsync(default) == 1, "one deletion failure isolated");
            Check(await Processor(scope.ServiceProvider).ProcessNextAsync(default), "extraction continues after deletion failure");
        }
        Check(!store.Files.ContainsKey(healthyKey) && store.Files.ContainsKey(poisonKey), "healthy object deleted despite poison");
        Check((await Get(next.Id)).Draft!.Warnings.Contains("date_uncertain"), "persisted draft retains date uncertainty");
        extract.Date = "2026-09-21";
        using (var scope = provider.CreateScope())
            Check(!(await scope.ServiceProvider.GetRequiredService<IReceiptRepository>().CleanupCandidatesAsync(clock.GetUtcNow(), default))
                .Any(x => x.Id == poisoned.Id), "failed deletion waits for retry");
        store.FailDelete = null;
        clock.Advance(TimeSpan.FromMinutes(1));
        // Atomic reservations fence competing cleanup workers.
        async Task<bool> ReserveCleanup()
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IReceiptRepository>().ExpireAsync(poisoned.Id, clock.GetUtcNow(), default);
        }
        Check((await Task.WhenAll(ReserveCleanup(), ReserveCleanup())).Count(x => x) == 1, "one cleanup claimant");
        clock.Advance(TimeSpan.FromMinutes(2));
        using (var scope = provider.CreateScope())
            await Processor(scope.ServiceProvider).CleanupAsync(default);
        Check(!store.Files.ContainsKey(poisonKey), "failed deletion eventually retries");

        user.UserId = f.MemberUserId;
        var departed = await Upload(Guid.NewGuid());
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE household_members SET left_at_utc = {now} WHERE id = {f.MemberId}");
                await Reject<NotFoundException>(() => Get(departed.Id));
                Check(await Claim(clock.GetUtcNow()) == null, "departed jobs not claimed");
                await Processor(scope.ServiceProvider).CleanupAsync(default);
            }
            finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE household_members SET left_at_utc = NULL WHERE id = {f.MemberId}"); user.UserId = f.OwnerUserId; }
        }
        clock.Advance(TimeSpan.FromDays(8));
        using (var scope = provider.CreateScope())
        {
            await Processor(scope.ServiceProvider).CleanupAsync(default);
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check(store.Files.Count == 0, "expiry deletes documents");
            Check(await db.Transactions.CountAsync() == before, "no ledger writes");
            await Reject<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE receipt_scans SET attempts = 4 WHERE id = {first.Id}"));
            await Reject<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE receipt_scans SET household_member_id = {Guid.NewGuid()} WHERE id = {first.Id}"));
        }
        Console.WriteLine("PASS receipt privacy, idempotency, leases, retries, discard, expiry and ledger isolation");
    }
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    private static async Task Reject<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
    private sealed class Documents : IReceiptDocumentStore
    {
        public readonly ConcurrentDictionary<string, byte[]> Files = new(); public int Puts;
        public TaskCompletionSource? PutStarted, PutRelease;
        public string? FailDelete;
        public async Task PutAsync(string key, byte[] bytes, CancellationToken ct)
        {
            PutStarted?.TrySetResult();
            if (PutRelease is not null) await PutRelease.Task.WaitAsync(ct);
            Files[key] = bytes; Interlocked.Increment(ref Puts);
        }
        public Task<byte[]> GetAsync(string key, CancellationToken ct) => Task.FromResult(Files[key]);
        public Task DeleteAsync(string key, CancellationToken ct)
        {
            if (key == FailDelete) throw new IOException("Injected deletion failure");
            Files.TryRemove(key, out _); return Task.CompletedTask;
        }
    }
    private sealed class Extractor : IReceiptExtractor
    {
        public bool Fail;
        public string Date = "2026-09-21";
        public Task<ReceiptDraft> ExtractAsync(byte[] bytes, CancellationToken ct)
            => Fail ? throw new ReceiptExtractionException("invalid_extraction", false)
                : Task.FromResult(new ReceiptDraft("Shop", Date, "EUR", "9", [new("Food", "10", "Item"), new("Discount", "-1", "Discount")], []));
    }
}
