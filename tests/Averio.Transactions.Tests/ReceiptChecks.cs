using System.Net;
using System.Text;
using System.Text.Json;
using Averio.Application.Common.Exceptions;
using Averio.Application.Receipts;
using Averio.Domain.Receipts;
using Averio.Infrastructure.Receipts;
using Microsoft.Extensions.Options;
using SkiaSharp;

internal static class ReceiptChecks
{
    public static async Task RunAsync()
    {
        await ReceiptWorkerChecks.RunAsync();
        var input = new ReceiptDraft("Bolt", "2026-10-26", "huf", "9000",
            [new("Élelmiszer", "10000", "Item"), new("Kedvezmény", "-1000", "Discount"), new("Kártya", "9000", "Payment")], []);
        var draft = ReceiptDraftValidation.Validate(input);
        Check(draft.Total == "9000.00" && draft.Currency == "HUF" && draft.Lines[1].Amount == "-1000.00", "exact amounts");
        Check(draft.Lines[2].Kind == "Payment" && draft.Warnings.Contains("review_required"), "payment stays separate");
        Check(ReceiptDraftValidation.Validate(input with { Date = "10/11/26" }).Date == null, "ambiguous date");
        foreach (var amount in new[] { "1,000", "1e3", "1.234" })
            await Reject<ReceiptExtractionException>(() => Task.FromResult(ReceiptDraftValidation.Validate(input with { Total = amount })));
        await Reject<ReceiptExtractionException>(() => Task.FromResult(ReceiptDraftValidation.Validate(input with { Lines = [new("X", "1", "Account")] })));
        await Reject<ReceiptExtractionException>(() => Task.FromResult(ReceiptDraftValidation.Validate(new(null, null, null, null, [], []))));
        await Reject<ReceiptExtractionException>(() => Task.FromResult(ReceiptDraftValidation.Validate(input with { Lines = Enumerable.Repeat(new ReceiptLine("X", "1", "Item"), 301).ToArray() })));
        var named = ReceiptDraftValidation.Validate(input with { Lines = [new("TS DHUS", "100", "Item", " Darált hús ")] });
        Check(named.Lines[0].Description == "TS DHUS" && named.Lines[0].SuggestedDescription == "Darált hús", "name suggestions retain printed evidence");
        Check(ReceiptDraftValidation.Validate(input with { Lines = [new("X", "1", "Item", new string('x', 161))] }).Lines[0].SuggestedDescription is null, "oversized name suggestion ignored");
        Check(ReceiptDraftValidation.Validate(input with { Lines = [new("X", "-1", "Discount", "Wrong name")] }).Lines[0].SuggestedDescription is null, "non-item suggestions ignored");
        var normalizer = new ReceiptImageNormalizer();
        using var bitmap = new SKBitmap(64, 48);
        bitmap.Erase(SKColors.White);
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var jpeg = await normalizer.NormalizeAsync(new MemoryStream(png.ToArray()), default);
        using var codec = SKCodec.Create(new MemoryStream(jpeg));
        Check(codec.EncodedFormat == SKEncodedImageFormat.Jpeg && codec.Info.Width == 64 && codec.Info.Height == 48, "normalized JPEG");
        Check(!Encoding.ASCII.GetString(jpeg).Contains("Exif"), "no EXIF");
        await Reject<BusinessRuleException>(() => normalizer.NormalizeAsync(new MemoryStream(Encoding.UTF8.GetBytes("<svg/>")), default));
        await Reject<BusinessRuleException>(() => normalizer.NormalizeAsync(new MemoryStream(new byte[ReceiptOptions.MaxUploadBytes + 1]), default));
        await Reject<BusinessRuleException>(() => normalizer.NormalizeAsync(new MemoryStream(jpeg[..(jpeg.Length / 2)]), default));
        var handler = new StubHttp(input);
        var extractor = new GeminiReceiptExtractor(new HttpClient(handler), Options.Create(new GeminiReceiptOptions { ApiKey = "test", Model = "test-model" }));
        Check((await extractor.ExtractAsync(jpeg, default)).Total == "9000", "provider returns raw observations");
        handler.Text = JsonSerializer.Serialize(input with { Date = "10/11/26", Warnings = ["provider_warning"] });
        var raw = await extractor.ExtractAsync(jpeg, default);
        Check(raw.Date == "10/11/26", "adapter preserves ambiguous date until application validation");
        var normalized = ReceiptDraftValidation.Validate(raw);
        Check(normalized.Date is null && normalized.Warnings.Contains("date_uncertain")
            && !normalized.Warnings.Contains("provider_warning"), "application retains uncertainty without trusting provider warnings");
        handler.Text = null;
        Check(handler.Request!.Contains("responseJsonSchema") && handler.Request.Contains("image/jpeg") && handler.Key == "test", "provider contract");
        using (var requestJson = JsonDocument.Parse(handler.Request!))
        {
            var linesSchema = requestJson.RootElement.GetProperty("generationConfig")
                .GetProperty("responseJsonSchema").GetProperty("properties").GetProperty("lines");
            Check(linesSchema.GetProperty("type").GetString() == "array"
                && !linesSchema.TryGetProperty("maxItems", out _), "provider schema omits rejected array bound");
            Check(linesSchema.GetProperty("items").GetProperty("required").GetArrayLength() == 4,
                "provider line structure remains required");
        }
        handler.Text = JsonSerializer.Serialize(input with {
            Lines = Enumerable.Repeat(new ReceiptLine("X", "1", "Item"), 301).ToArray()
        });
        var oversized = await extractor.ExtractAsync(jpeg, default);
        await Reject<ReceiptExtractionException>(() => Task.FromResult(ReceiptDraftValidation.Validate(oversized)));
        handler.Text = null;
        handler.Status = HttpStatusCode.TooManyRequests;
        try { await extractor.ExtractAsync(jpeg, default); throw new Exception("429 accepted"); }
        catch (ReceiptExtractionException e) { Check(e.Transient, "429 retryable"); }
        handler.Status = HttpStatusCode.OK; handler.Finish = "MAX_TOKENS";
        await Reject<ReceiptExtractionException>(() => extractor.ExtractAsync(jpeg, default));
        handler.Finish = "STOP"; handler.Text = "```json {} ```";
        await Reject<ReceiptExtractionException>(() => extractor.ExtractAsync(jpeg, default));
        var now = DateTimeOffset.UtcNow;
        var scan = ReceiptScan.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), jpeg.Length, now);
        Check(scan.Status == ReceiptScanStatus.Uploading && scan.ExpiresAt == now.AddDays(7) && scan.Attempts == 0, "initial lifecycle");
    }
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    private static async Task Reject<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
    private sealed class StubHttp(ReceiptDraft draft) : HttpMessageHandler
    {
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Finish = "STOP";
        public string? Text, Request, Key;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = await request.Content!.ReadAsStringAsync(ct);
            Key = request.Headers.GetValues("x-goog-api-key").Single();
            return new(Status) { Content = new StringContent(JsonSerializer.Serialize(new { candidates = new[] {
                new { finishReason = Finish, content = new { parts = new[] { new { text = Text ?? JsonSerializer.Serialize(draft) } } } }
            } })) };
        }
    }
}
