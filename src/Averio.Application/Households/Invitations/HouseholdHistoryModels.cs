namespace Averio.Application.Households.Invitations;

public sealed record HistorySummary(Guid Id, string HouseholdName, DateTimeOffset LeftAtUtc, bool WasLastMember);
public sealed record HistoryAccount(Guid Id, string Name, string Currency, string Type, string Scope,
    string Visibility, bool IncludeInHouseholdReports, string Balance, bool WasCopied);
// Null preserves compatibility with snapshots captured before category classification.
public sealed record HistoryItem(string Description, string CategoryName, string Amount, bool? IsSystemCategory = null);
public sealed record HistoryTransaction(Guid Id, Guid AccountId, string Description, string? Merchant,
    string? Note, DateOnly OccurredOn, string Amount, string Currency, string Kind, bool IsVoided,
    bool IncludedInHouseholdReports, IReadOnlyList<HistoryItem> Items);
public sealed record HistoryTransferSide(Guid AccountId, string AccountName, string Currency, string Amount);
public sealed record HistoryTransfer(Guid Id, DateOnly OccurredOn, HistoryTransferSide? Source,
    HistoryTransferSide? Destination, string? Note, bool IsVoided);
public sealed record HistoryCashFlow(string Currency, string Income, string Expense);
public sealed record HistorySnapshot(HistorySummary Summary, IReadOnlyList<HistoryAccount> Accounts,
    IReadOnlyList<HistoryTransaction> Transactions, IReadOnlyList<HistoryTransfer> Transfers, IReadOnlyList<HistoryCashFlow> CashFlow);
public sealed record FormerMember(Guid Id, string? DisplayName, DateTimeOffset LeftAtUtc);
public interface IHouseholdHistoryRepository
{
    Task<IReadOnlyList<HistorySummary>> ListAsync(Guid userId, CancellationToken ct);
    Task<HistorySnapshot?> GetAsync(Guid userId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<FormerMember>> FormerMembersAsync(Guid householdId, CancellationToken ct);
}
