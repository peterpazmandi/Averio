using Averio.Domain.Imports;
namespace Averio.Application.Imports;

public sealed record ImportRow(string Sheet, int RowNumber, string Kind, DateOnly Date,
    string Account, string? Destination, string Category, decimal Amount, string Currency,
    decimal? ReceivedAmount, string? ReceivedCurrency, string Tags, string Note);
public sealed record ImportAccountMapping(string Source, Guid AccountId, string Visibility);
public sealed record ImportCategoryMapping(string Kind, string Source, Guid? CategoryId);
public sealed record ConfirmImportRequest(IReadOnlyList<ImportAccountMapping> Accounts,
    IReadOnlyList<ImportCategoryMapping> Categories, bool AcknowledgeBalancesAndOverlap);
public sealed record ImportCategorySource(string Kind, string Name);
public sealed record ImportTotal(string Kind, string Currency, string Amount, int Count);
public sealed record ImportPreviewRow(string Sheet, int RowNumber, string Kind, DateOnly Date,
    string Account, string? Destination, string Category, string Amount, string Currency,
    string? ReceivedAmount, string? ReceivedCurrency, string Tags, string Note);
public sealed record ImportPreview(Guid Id, string Status, int Count, DateOnly From, DateOnly To,
    IReadOnlyList<string> Accounts, IReadOnlyList<ImportCategorySource> Categories,
    IReadOnlyList<ImportTotal> Totals, int RepeatedRows, IReadOnlyList<ImportPreviewRow> Rows);
public sealed record ImportResult(Guid Id, string Status, int Transactions, int Transfers);
public interface IXlsxImportReader { IReadOnlyList<ImportRow> Read(Stream stream); }
public interface IImportRepository
{
    void Add(ImportBatch batch);
    Task<ImportBatch?> FindAsync(Guid householdId, Guid memberId, string fingerprint, CancellationToken ct);
    Task<ImportBatch?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
    Task ApplyAggregatesAsync(Guid[] transactionIds, CancellationToken ct);
}
