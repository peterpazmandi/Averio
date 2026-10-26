using Averio.Application.Receipts;
namespace Averio.Infrastructure.Receipts;

internal sealed class DisabledReceiptServices : IReceiptDocumentStore, IReceiptExtractor
{
    private static Exception Disabled() => new ReceiptExtractionException("receipt_disabled", false);
    public Task PutAsync(string key, byte[] jpeg, CancellationToken ct) => throw Disabled();
    public Task<byte[]> GetAsync(string key, CancellationToken ct) => throw Disabled();
    public Task DeleteAsync(string key, CancellationToken ct) => throw Disabled();
    public Task<ReceiptDraft> ExtractAsync(byte[] jpeg, CancellationToken ct) => throw Disabled();
}
