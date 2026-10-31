using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Receipts;
using Averio.Infrastructure.Receipts;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

internal static class ReceiptRolloutVerification
{
    public static async Task RunAsync(ServiceProvider provider, MutableCurrentUser user, Fixture f, DateTimeOffset now)
    {
        var clock = new MutableTimeProvider(now);
        var options = new ReceiptOptions { Enabled = true, DailyUploadLimit = 100, RetainPostedImages = true };
        var documents = new Documents();
        using var bitmap = new SKBitmap(64, 48); bitmap.Erase(SKColors.Blue);
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var image = png.ToArray();
        ReceiptHandler Handler(IServiceProvider s) => new(user, s.GetRequiredService<IHouseholdRepository>(),
            s.GetRequiredService<IReceiptRepository>(), new ReceiptImageNormalizer(), documents, s.GetRequiredService<IUnitOfWork>(), options, clock);
        async Task<ReceiptScanResult> Upload(Guid op)
        { using var s = provider.CreateScope(); return await Handler(s.ServiceProvider).UploadAsync(f.HouseholdId, op, new MemoryStream(image), default); }
        async Task<ReceiptCapabilities> Usage()
        { using var s = provider.CreateScope(); return await Handler(s.ServiceProvider).CapabilitiesAsync(f.HouseholdId, default); }
        user.UserId = f.OwnerUserId;
        var initial = await Usage();
        options.FreeMonthlyLimit = initial.MonthlyUsed + 1;
        async Task<ReceiptScanResult?> Admit(Guid op)
        { try { return await Upload(op); } catch (BusinessRuleException ex) when (ex.Code == "receipt_monthly_limit") { return null; } }
        var operations = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var results = await Task.WhenAll(operations.Select(Admit));
        Check(results.Count(x => x is not null) == 1 && (await Usage()).MonthlyUsed == initial.MonthlyUsed + 1, "Concurrent admission exceeded quota");
        var index = Array.FindIndex(results, x => x is not null);
        var accepted = results[index]!;
        options.Enabled = false;
        Check((await Upload(operations[index])).Id == accepted.Id && documents.Puts == 1, "Lost-response retry while disabled/quota exhausted was charged twice");
        options.Enabled = true;
        options.PaidMonthlyLimit = initial.MonthlyUsed + 5;
        options.PaidHouseholds[f.HouseholdId] = now.AddHours(1);
        var second = await Upload(Guid.NewGuid());
        using (var scope = provider.CreateScope())
        {
            var handler = Handler(scope.ServiceProvider);
            Check((await handler.GetAsync(f.HouseholdId, second.Id, default)).PossibleDuplicate, "Same uploader duplicate missing");
            Check((await handler.ListAsync(f.HouseholdId, 0, default)).Single(s => s.Id == accepted.Id).PossibleDuplicate, "Inbox duplicate missing");
        }
        user.UserId = f.MemberUserId;
        var privateScan = await Upload(Guid.NewGuid());
        Check(!privateScan.PossibleDuplicate, "Duplicate warning disclosed another member's image");
        using (var scope = provider.CreateScope())
        {
            var handler = Handler(scope.ServiceProvider);
            await Reject<NotFoundException>(() => handler.GetAsync(f.HouseholdId, accepted.Id, default));
            await Reject<NotFoundException>(() => handler.DeleteImageAsync(f.HouseholdId, accepted.Id, accepted.Version, default));
        }
        user.UserId = f.OwnerUserId;
        options.PaidHouseholds[f.HouseholdId] = now;
        Check(await Admit(Guid.NewGuid()) is null, "Expired entitlement allowed another upload");
        var reset = ReceiptOptions.MonthStart(now).AddMonths(1);
        clock.Advance(reset - now);
        Check((await Usage()).MonthlyUsed == 0 && (await Usage()).ResetsAt == reset.AddMonths(1), "UTC monthly reset excludes prior month");
        Check(await Admit(Guid.NewGuid()) is not null, "New month did not restore allowance");
        Console.WriteLine("PASS receipt monthly quota race, retry, entitlement expiry/reset and uploader-only duplicate warnings");
    }
    private sealed class Documents : IReceiptDocumentStore
    {
        public int Puts;
        public Task PutAsync(string key, byte[] data, CancellationToken ct) { Interlocked.Increment(ref Puts); return Task.CompletedTask; }
        public Task<byte[]> GetAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Reject<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
}
