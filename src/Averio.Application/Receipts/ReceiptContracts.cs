using Averio.Domain.Receipts;
namespace Averio.Application.Receipts;

public sealed class ReceiptOptions
{
    public bool Enabled { get; set; }
    public int DailyUploadLimit { get; set; } = 20;
    public int FreeMonthlyLimit { get; set; } = 10;
    public int PaidMonthlyLimit { get; set; } = 100;
    // Server-managed, time-bounded entitlements. Never accepted from the browser.
    public Dictionary<Guid, DateTimeOffset> PaidHouseholds { get; set; } = [];
    public bool RetainPostedImages { get; set; }
    public int PostedImageRetentionDays { get; set; } = 365;
    public int MonthlyLimit(Guid household, DateTimeOffset now)
        => PaidHouseholds.TryGetValue(household, out var until) && until > now ? PaidMonthlyLimit : FreeMonthlyLimit;
    public static DateTimeOffset MonthStart(DateTimeOffset now)
        => new(now.UtcDateTime.Year, now.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero);
    public const int MaxUploadBytes = 10 * 1024 * 1024;
    public const int MaxAttempts = 3;
}
// Exact decimal strings. Observations, not trusted ledger inputs.
public sealed record ReceiptLine(string Description, string? Amount, string Kind, string? SuggestedDescription = null,
    Guid? SuggestedCategoryId = null, string? CategorySuggestionSource = null);
public sealed record ReceiptDraft(string? Merchant, string? Date, string? Currency,
    string? Total, ReceiptLine[] Lines, string[] Warnings);
public sealed record ReceiptScanResult(Guid Id, string Status, Guid Version, int Attempts,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string? FailureCode, ReceiptDraft? Draft,
    Guid? PostedTransactionId = null, bool ImageAvailable = false, bool PossibleDuplicate = false);
public sealed record ReceiptCapabilities(bool Enabled, int MaxUploadBytes, int MonthlyLimit = 0,
    int MonthlyUsed = 0, DateTimeOffset? ResetsAt = null, int PostedImageRetentionDays = 0);
public interface IReceiptImageNormalizer { Task<byte[]> NormalizeAsync(Stream input, CancellationToken ct); }
public interface IReceiptDocumentStore
{
    Task PutAsync(string key, byte[] jpeg, CancellationToken ct);
    Task<byte[]> GetAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
public interface IReceiptExtractor
{
    Task<ReceiptDraft> ExtractAsync(byte[] jpeg, CancellationToken ct);
    Task<ReceiptDraft> ExtractAsync(byte[] jpeg, IReadOnlyList<ReceiptCategoryOption> categories, CancellationToken ct)
        => ExtractAsync(jpeg, ct);
}
public sealed class ReceiptExtractionException(string code, bool transient) : Exception(code)
{
    public string Code { get; } = code;
    public bool Transient { get; } = transient;
}
public interface IReceiptRepository
{
    Task<ReceiptScan?> FindOperationAsync(Guid household, Guid member, Guid operation, CancellationToken ct);
    Task<int> CountUploadsAsync(Guid household, Guid member, DateTimeOffset since, CancellationToken ct);
    Task<int> CountMonthlyUploadsAsync(Guid household, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<IReadOnlySet<string>> DuplicateHashesAsync(Guid household, Guid member, string[] hashes, DateTimeOffset now, CancellationToken ct);
    Task<bool> DeletePostedImageAsync(Guid id, Guid version, DateTimeOffset now, CancellationToken ct);
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

