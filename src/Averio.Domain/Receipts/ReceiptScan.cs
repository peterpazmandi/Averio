namespace Averio.Domain.Receipts;

// Private temporary processing state, never a ledger entry.
public sealed class ReceiptScan
{
    private ReceiptScan() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid HouseholdMemberId { get; private set; }
    public Guid OperationId { get; private set; }
    public string ContentHash { get; private set; } = "";
    public string ObjectKey { get; private set; } = "";
    public int ByteLength { get; private set; }
    public ReceiptScanStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public Guid Version { get; private set; }
    public Guid? PostedTransactionId { get; private set; }
    public string? ConfirmationFingerprint { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public string? DraftJson { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? DocumentDeletedAt { get; private set; }

    public static ReceiptScan Create(Guid household, Guid member, Guid operation, string hash, int bytes, DateTimeOffset now, bool retainable = false)
    {
        if (household == Guid.Empty || member == Guid.Empty || operation == Guid.Empty
            || hash.Length != 64 || bytes <= 0 || bytes > 10485760) throw new ArgumentException("Invalid receipt metadata.");
        var id = Guid.NewGuid();
        return new ReceiptScan { Id = id, HouseholdId = household, HouseholdMemberId = member,
            OperationId = operation, ContentHash = hash, ByteLength = bytes,
            ObjectKey = $"{(retainable ? "receipt-images" : "receipt-drafts")}/{household:N}/{member:N}/{id:N}.jpg",
            Status = ReceiptScanStatus.Uploading, Version = Guid.NewGuid(), CreatedAt = now,
            UpdatedAt = now, NextAttemptAt = now, ExpiresAt = now.AddDays(7) };
    }
}
public enum ReceiptScanStatus { Uploading, Queued, Processing, Ready, Failed, Discarded, Posted }
