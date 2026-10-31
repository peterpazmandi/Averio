using System.Net;
using Averio.Application.Receipts;
using Averio.Infrastructure.Receipts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal static class ReceiptTimeoutChecks
{
    public static async Task RunAsync()
    {
        if (ReceiptTimeouts.ProviderRequest != TimeSpan.FromSeconds(50)
            || ReceiptTimeouts.Processing != TimeSpan.FromSeconds(60))
            throw new Exception("Receipt timeout budgets changed; review the production/benchmark boundary.");

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Receipts:Storage:Bucket"] = "timeout-test",
            ["Receipts:Storage:Region"] = "us-east-1",
            ["Receipts:Gemini:ApiKey"] = "test",
            ["Receipts:Gemini:Model"] = "test-model"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReceiptServices(configuration);
        // Exercise the real production registration, replacing only network transport.
        services.AddHttpClient<IReceiptExtractor, GeminiReceiptExtractor>()
            .ConfigurePrimaryHttpMessageHandler(() => new DelayedHeaders());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var production = scope.ServiceProvider.GetRequiredService<IReceiptExtractor>();
        using var benchmarkHttp = ReceiptTimeouts.CreateProviderClient(new DelayedHeaders());
        var benchmark = new GeminiReceiptExtractor(benchmarkHttp,
            Options.Create(new GeminiReceiptOptions { ApiKey = "test", Model = "test-model" }));
        // A correct response at 55 seconds passed the old benchmark but timed out
        // in production. Both requests run concurrently, with no external calls.
        await Task.WhenAll(ExpectHttpTimeout(production), ExpectHttpTimeout(benchmark));
    }

    private static async Task ExpectHttpTimeout(IReceiptExtractor extractor)
    {
        using var deadline = ReceiptTimeouts.CreateProcessingCancellation();
        try
        {
            await extractor.ExtractAsync([1], deadline.Token);
        }
        catch (OperationCanceledException) when (!deadline.IsCancellationRequested)
        {
            return; // The 50-second HTTP budget fired before the 60-second outer deadline.
        }
        throw new Exception("A provider response beyond the production HTTP budget was accepted.");
    }

    private sealed class DelayedHeaders : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(55), ct);
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("""
                    {"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"merchant\":\"Shop\",\"date\":\"2026-10-30\",\"currency\":\"EUR\",\"total\":\"9.00\",\"lines\":[{\"description\":\"Milk\",\"amount\":\"9.00\",\"kind\":\"Item\"}]}"}]}}]}
                    """)
            };
        }
    }
}
