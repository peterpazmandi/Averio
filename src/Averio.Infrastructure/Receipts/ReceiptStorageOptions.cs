namespace Averio.Infrastructure.Receipts;

public sealed class ReceiptStorageOptions
{
    public string Bucket { get; set; } = "";
    public string Region { get; set; } = "eu-central-1";
}
public sealed class GeminiReceiptOptions
{
    public string ApiKey { get; set; } = "";
    // Explicitly selected after benchmarking. No silent model upgrades/default spend.
    public string Model { get; set; } = "";
}
