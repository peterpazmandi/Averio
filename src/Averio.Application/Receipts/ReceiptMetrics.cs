using System.Diagnostics.Metrics;
namespace Averio.Application.Receipts;

// Bounded tags only: never include user/household IDs, merchant, image or provider text.
public static class ReceiptMetrics
{
    public const string MeterName = "Averio.Receipts";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly Counter<long> Uploads = Meter.CreateCounter<long>("averio.receipts.uploads", "{upload}");
    public static readonly Counter<long> Extractions = Meter.CreateCounter<long>("averio.receipts.extractions", "{attempt}");
    public static readonly Counter<long> Confirmations = Meter.CreateCounter<long>("averio.receipts.confirmations", "{confirmation}");
    public static readonly Counter<long> Cleanup = Meter.CreateCounter<long>("averio.receipts.cleanup", "{image}");
    public static readonly Counter<long> Tokens = Meter.CreateCounter<long>("averio.receipts.provider.tokens", "{token}");
    public static readonly Histogram<double> ProcessingSeconds = Meter.CreateHistogram<double>("averio.receipts.processing.duration", "s");
    public static readonly Histogram<double> QueueAgeSeconds = Meter.CreateHistogram<double>("averio.receipts.queue.age", "s");
}
