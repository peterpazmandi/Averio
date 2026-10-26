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
        user.UserId = f.MemberUserId;
        var departed = await Upload(Guid.NewGuid());
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE household_members SET left_at_utc = {now} WHERE id = {f.MemberId}");
                await Reject<NotFoundException>(() => Get(departed.Id));
                Check(await Claim(now) == null, "departed jobs not claimed");
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
        public Task PutAsync(string key, byte[] bytes, CancellationToken ct) { Files[key] = bytes; Interlocked.Increment(ref Puts); return Task.CompletedTask; }
        public Task<byte[]> GetAsync(string key, CancellationToken ct) => Task.FromResult(Files[key]);
        public Task DeleteAsync(string key, CancellationToken ct) { Files.TryRemove(key, out _); return Task.CompletedTask; }
    }
    private sealed class Extractor : IReceiptExtractor
    {
        public bool Fail;
        public Task<ReceiptDraft> ExtractAsync(byte[] bytes, CancellationToken ct)
            => Fail ? throw new ReceiptExtractionException("invalid_extraction", false)
                : Task.FromResult(new ReceiptDraft("Shop", "2026-09-21", "EUR", "9", [new("Food", "10", "Item"), new("Discount", "-1", "Discount")], []));
    }
}
