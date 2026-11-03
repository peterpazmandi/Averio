using Averio.Application.Receipts;
using Averio.Domain.Receipts;
using System.Diagnostics.Metrics;

internal static class ReceiptRolloutChecks
{
    public static void Run()
    {
        var h = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero);
        var options = new ReceiptOptions { FreeMonthlyLimit = 0, PaidMonthlyLimit = 100 };
        Check(options.MonthlyLimit(h, now) == 0, "No implicit paid access");
        options.PaidHouseholds[h] = now.AddMinutes(30);
        Check(options.MonthlyLimit(h, now) == 100 && options.MonthlyLimit(Guid.NewGuid(), now) == 0, "Household entitlement isolation");
        Check(options.MonthlyLimit(h, now.AddMinutes(30)) == 0, "Paid access expires at boundary");
        Check(ReceiptOptions.MonthStart(now.ToOffset(TimeSpan.FromHours(2))) == new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), "UTC month, not local date");
        Check(ReceiptOptions.MonthStart(now).AddMonths(1).Year == 2027, "December reset");
        var legacy = ReceiptScan.Create(h, Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), 1, now);
        var retained = ReceiptScan.Create(h, Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), 1, now, true);
        Check(legacy.ObjectKey.StartsWith("receipt-drafts/") && retained.ObjectKey.StartsWith("receipt-images/"), "Lifecycle prefixes separated");
        Check(retained.ExpiresAt == now.AddDays(7), "Unconfirmed drafts still expire after seven days");
        long observed = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) => { if (instrument.Meter.Name == ReceiptMetrics.MeterName) l.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => {
            if (instrument.Name == "averio.receipts.uploads") observed += value;
            foreach (var tag in tags) Check(tag.Key is "outcome" or "kind", "Unexpected metric dimension");
        });
        listener.Start();
        ReceiptMetrics.Uploads.Add(1, new KeyValuePair<string, object?>("outcome", "admitted"));
        Check(observed == 1, "Receipt meter can be collected");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
