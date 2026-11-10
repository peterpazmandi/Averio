using Averio.Domain.Imports;
namespace Averio.Application.Imports;

public sealed record ImportRow(string Sheet, int RowNumber, string Kind, DateOnly Date,
    string Account, string? Destination, string Category, decimal Amount, string Currency,
    decimal? ReceivedAmount, string? ReceivedCurrency, string Tags, string Note);
public sealed record ImportSourceAccount(string Source, string Currency, string? OpeningBalance, DateOnly? OpeningBalanceDate);
public sealed record YnabImportReadResult(IReadOnlyList<ImportRow> Rows, IReadOnlyList<ImportSourceAccount> SourceAccounts);
public sealed record ImportAccountMapping(string Source, Guid AccountId, string Visibility);
public sealed record ImportCategoryMapping(string Kind, string Source, Guid? CategoryId);
public sealed record ConfirmImportRequest(IReadOnlyList<ImportAccountMapping> Accounts,
    IReadOnlyList<ImportCategoryMapping> Categories, bool AcknowledgeBalancesAndOverlap, string? ReviewToken = null, IReadOnlyList<int>? SkippedRows = null);
public sealed record ImportCategorySource(string Kind, string Name);
public sealed record ImportTotal(string Kind, string Currency, string Amount, int Count);
public sealed record ImportPreviewRow(string Sheet, int RowNumber, string Kind, DateOnly Date,
    string Account, string? Destination, string Category, string Amount, string Currency,
    string? ReceivedAmount, string? ReceivedCurrency, string Tags, string Note);
public sealed record ImportPreview(Guid Id, string Status, int Count, DateOnly From, DateOnly To,
    IReadOnlyList<string> Accounts, IReadOnlyList<ImportSourceAccount> SourceAccounts, IReadOnlyList<ImportCategorySource> Categories,
    IReadOnlyList<ImportTotal> Totals, int RepeatedRows, IReadOnlyList<ImportPreviewRow> Rows, ImportMappingDefaults? Defaults = null, ImportResult? Completion = null);
public sealed record ImportResult(Guid Id, string Status, int Transactions, int Transfers, int Accounts = 0, int Skipped = 0);
public interface IXlsxImportReader { IReadOnlyList<ImportRow> Read(Stream stream); }
public interface IYnabImportReader
{
    YnabImportReadResult Read(Stream stream, string fileName, string currency, string dateFormat, string numberFormat);
}
public interface IImportRepository
{
    Task<IReadOnlyList<ImportHistoryStored>> ListCompletedAsync(Guid householdId, Guid memberId, int page, CancellationToken ct);
    Task<IReadOnlyList<ImportCurrentRecord>> GetCurrentRecordsAsync(Guid householdId, Guid memberId, Guid[] transactionIds, Guid[] transferIds, CancellationToken ct);
    void Add(ImportBatch batch);
    Task<ImportBatch?> FindAsync(Guid householdId, Guid memberId, string fingerprint, CancellationToken ct);
    Task<ImportBatch?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
    Task ApplyAggregatesAsync(Guid[] transactionIds, CancellationToken ct, int direction = 1);
    Task<IReadOnlyList<ImportBatch>> RecentMappedAsync(Guid householdId, Guid memberId, CancellationToken ct);
    Task<IReadOnlyList<ImportExistingRecord>> FindOverlapAsync(Guid householdId, Guid memberId, Guid[] accountIds, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlySet<Guid>> LinkedRecordsAsync(Guid householdId, Guid[] ids, CancellationToken ct);
    Task VoidRecordsAsync(Guid householdId, Guid[] transactionIds, Guid[] transferIds, DateTimeOffset now, CancellationToken ct);
}
