namespace Averio.Application.Receipts;

// Shared by the worker's HTTP registration and the live benchmark. A benchmark
// result must not be accepted with a more generous deadline than production.
public static class ReceiptTimeouts
{
    public static readonly TimeSpan ProviderRequest = TimeSpan.FromSeconds(50);
    public static readonly TimeSpan Processing = TimeSpan.FromSeconds(60);

    public static void ConfigureProviderClient(HttpClient client) => client.Timeout = ProviderRequest;

    public static HttpClient CreateProviderClient(HttpMessageHandler? handler = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        ConfigureProviderClient(client);
        return client;
    }

    public static CancellationTokenSource CreateProcessingCancellation(CancellationToken ct = default)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        source.CancelAfter(Processing);
        return source;
    }
}
