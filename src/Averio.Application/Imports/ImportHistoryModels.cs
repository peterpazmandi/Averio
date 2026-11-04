namespace Averio.Application.Imports;

// Array order matches the immutable source rows; never infer links from amounts or dates.
public sealed record ImportRecordLink(Guid Id, string Kind, DateTimeOffset Version,
    string Account, string? Destination, string? Category, bool CategoryIsBase);
public sealed record ImportHistorySummary(int Count, DateOnly From, DateOnly To, IReadOnlyList<ImportTotal> Totals);
public sealed record ImportHistoryItem(Guid Id, string? SourceName, DateTimeOffset CompletedAt,
    ImportHistorySummary Summary, bool TrackingAvailable);
public sealed record ImportHistoryPage(IReadOnlyList<ImportHistoryItem> Items, bool HasMore);
public sealed record ImportHistoryRecord(ImportPreviewRow Source, Guid? RecordId, string State,
    bool? Changed, bool CanModify, string? ImportedAccount, string? ImportedDestination,
    string? ImportedCategory, bool CategoryIsBase);
public sealed record ImportUndoSummary(int Unchanged, int Edited, int Voided, int Unavailable, int Untracked, int ReadOnly);
public sealed record ImportHistoryDetail(ImportHistoryItem Batch, IReadOnlyList<ImportHistoryRecord> Records,
    ImportUndoSummary Review, DateTimeOffset CheckedAt, bool HasMore);

public sealed record ImportCurrentRecord(Guid Id, bool IsTransfer, DateTimeOffset Version, bool Voided, bool CanModify);
public sealed record ImportHistoryStored(Guid Id, string? SourceName, DateTimeOffset CompletedAt,
    string? Summary, string? LegacyPayload, bool TrackingAvailable);
