using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using Averio.Application.Receipts;
using Averio.Infrastructure.Receipts;
using Microsoft.Extensions.Options;

// Deliberate live execution only; never run from CI. Keep fixtures/results out of Git.
if (args.Length != 3 || args[0] != "--live")
{
    Console.Error.WriteLine("Usage: --live <private-manifest.json> <private-results.json>. This makes billable Gemini calls.");
    return 2;
}
var key = Environment.GetEnvironmentVariable("Receipts__Gemini__ApiKey");
var model = Environment.GetEnvironmentVariable("Receipts__Gemini__Model");
if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(model))
{
    Console.Error.WriteLine("Set Receipts__Gemini__ApiKey and Receipts__Gemini__Model.");
    return 2;
}
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var manifest = Path.GetFullPath(args[1]);
var samples = JsonSerializer.Deserialize<Sample[]>(await File.ReadAllTextAsync(manifest), jsonOptions) ?? [];
if (samples.Length is < 30 or > 50 || new[] { "hu", "de", "en" }.Any(l => samples.Count(s => s.Language == l) < 10)
    || samples.Select(s => s.Id).Distinct().Count() != samples.Length
    || samples.Any(s => string.IsNullOrWhiteSpace(s.Id) || !new[] { "hu", "de", "en" }.Contains(s.Language)
        || string.IsNullOrWhiteSpace(s.Image) || !File.Exists(Path.Combine(Path.GetDirectoryName(manifest)!, s.Image))
        || !DateOnly.TryParseExact(s.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
        || !new[] { "HUF", "EUR", "USD" }.Contains(s.Currency) || !Amount(s.Total, out var total) || total <= 0))
{
    Console.Error.WriteLine("Require 30–50 unique samples, at least 10 per HU/DE/EN, existing images and verified date/currency/positive total labels.");
    return 2;
}
using var http = ReceiptTimeouts.CreateProviderClient();
var extractor = new GeminiReceiptExtractor(http, Options.Create(new GeminiReceiptOptions { ApiKey = key, Model = model }));
var normalizer = new ReceiptImageNormalizer();
long tokens = 0;
using var listener = new MeterListener();
listener.InstrumentPublished = (instrument, l) => {
    if (instrument.Meter.Name == ReceiptMetrics.MeterName && instrument.Name == "averio.receipts.provider.tokens") l.EnableMeasurementEvents(instrument);
};
listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref tokens, value));
listener.Start();
var results = new List<Result>();
foreach (var sample in samples)
{
    var start = Stopwatch.GetTimestamp();
    var before = tokens;
    var result = new Result(sample.Id, sample.Language, false, false, false, 0, 0, "failed");
    try
    {
        using var timeout = ReceiptTimeouts.CreateProcessingCancellation();
        await using var input = File.OpenRead(Path.Combine(Path.GetDirectoryName(manifest)!, sample.Image));
        var image = await normalizer.NormalizeAsync(input, timeout.Token);
        var actual = ReceiptDraftValidation.Validate(await extractor.ExtractAsync(image, timeout.Token));
        result = result with { TotalCorrect = Amount(actual.Total, out var observed) && Amount(sample.Total, out var expected) && observed == expected,
            DateCorrect = actual.Date == sample.Date, CurrencyCorrect = actual.Currency == sample.Currency, Outcome = "extracted" };
    }
    catch (Exception ex) when (ex is not OutOfMemoryException) { /* Never log provider/image content. A failed sample fails all fields. */ }
    results.Add(result with { Seconds = Stopwatch.GetElapsedTime(start).TotalSeconds, Tokens = tokens - before });
    // Checkpoint after every paid call; avoid repeated calls after interruption.
    await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(new { Model = model, At = DateTimeOffset.UtcNow, Complete = results.Count == samples.Length, Results = results }, jsonOptions));
}
var passed = new[] { "hu", "de", "en" }.All(l => {
    var group = results.Where(r => r.Language == l).ToArray();
    return group.Count(r => r.TotalCorrect) >= Math.Ceiling(group.Length * .95)
        && group.Count(r => r.DateCorrect) >= Math.Ceiling(group.Length * .95) && group.All(r => r.CurrencyCorrect);
});
Console.WriteLine(passed ? "PASS extraction field accuracy gate; manual review and operational rollout checks remain required." : "FAIL extraction field accuracy gate. Inspect the private results.");
return passed ? 0 : 1;

static bool Amount(string? value, out decimal amount) => decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount);
internal sealed record Sample(string Id, string Language, string Image, string Date, string Currency, string Total);
internal sealed record Result(string Id, string Language, bool TotalCorrect, bool DateCorrect, bool CurrencyCorrect, double Seconds, long Tokens, string Outcome);
