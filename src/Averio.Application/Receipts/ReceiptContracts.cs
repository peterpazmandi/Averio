using Averio.Domain.Receipts;
namespace Averio.Application.Receipts;

public sealed class ReceiptOptions
{
    public bool Enabled { get; set; }
    public int DailyUploadLimit { get; set; } = 20;
    public const int MaxUploadBytes = 10 * 1024 * 1024;
    public const int MaxAttempts = 3;
}
// Exact decimal strings. Observations, not trusted ledger inputs.
public sealed record ReceiptLine(string Description, string? Amount, string Kind, string? SuggestedDescription = null);
public sealed record ReceiptDraft(string? Merchant, string? Date, string? Currency,
    string? Total, ReceiptLine[] Lines, string[] Warnings);
public sealed record ReceiptScanResult(Guid Id, string Status, Guid Version, int Attempts,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string? FailureCode, ReceiptDraft? Draft,
    Guid? PostedTransactionId = null);
public sealed record ReceiptCapabilities(bool Enabled, int MaxUploadBytes);
public interface IReceiptImageNormalizer { Task<byte[]> NormalizeAsync(Stream input, CancellationToken ct); }
public interface IReceiptDocumentStore
{
    Task PutAsync(string key, byte[] jpeg, CancellationToken ct);
    Task<byte[]> GetAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
public interface IReceiptExtractor { Task<ReceiptDraft> ExtractAsync(byte[] jpeg, CancellationToken ct); }
public sealed class ReceiptExtractionException(string code, bool transient) : Exception(code)
{
    public string Code { get; } = code;
    public bool Transient { get; } = transient;
}
public interface IReceiptRepository
{
    Task<ReceiptScan?> FindOperationAsync(Guid household, Guid member, Guid operation, CancellationToken ct);
    Task<int> CountUploadsAsync(Guid household, Guid member, DateTimeOffset since, CancellationToken ct);
    void Add(ReceiptScan scan);
    Task<bool> QueueAsync(Guid id, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<ReceiptScan>> ListAsync(Guid household, Guid member, int skip, CancellationToken ct);
    Task<ReceiptScan?> GetAsync(Guid household, Guid member, Guid id, CancellationToken ct);
    Task<bool> DiscardAsync(Guid id, Guid version, DateTimeOffset now, CancellationToken ct);
    Task<bool> PostAsync(Guid id, Guid version, Guid transactionId, string confirmationFingerprint, DateTimeOffset now, CancellationToken ct);
    Task<ReceiptScan?> ClaimAsync(DateTimeOffset now, CancellationToken ct);
    Task<bool> FinishAsync(Guid id, Guid token, DateTimeOffset now, string? draft, string? failure, bool transient, CancellationToken ct);
    Task<IReadOnlyList<ReceiptScan>> CleanupCandidatesAsync(DateTimeOffset now, CancellationToken ct);
    Task<bool> ExpireAsync(Guid id, DateTimeOffset now, CancellationToken ct);
    Task DeferCleanupAsync(Guid id, DateTimeOffset retryAt, CancellationToken ct);
    Task MarkDocumentDeletedAsync(Guid id, DateTimeOffset now, CancellationToken ct);
}
