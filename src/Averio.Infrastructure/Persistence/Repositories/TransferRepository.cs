using Averio.Application.Transfers;
using Averio.Domain.Accounts;
using Averio.Domain.Transfers;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class TransferRepository(AverioDbContext db) : ITransferRepository
{
    public void Add(Transfer transfer) => db.Transfers.Add(transfer);

    private IQueryable<Account> VisibleAccounts(Guid householdId, Guid memberId)
        => db.Accounts.Where(a => a.HouseholdId == householdId
            && (a.Visibility == AccountVisibility.Household
                || a.Owners.Any(o => o.HouseholdMemberId == memberId)));

    public Task<Transfer?> GetForUpdateAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct)
    {
        var writable = db.Accounts.Where(a => a.HouseholdId == householdId
            && (a.Scope == AccountScope.Household || a.Owners.Any(o => o.HouseholdMemberId == memberId)));
        return db.Transfers.SingleOrDefaultAsync(t => t.HouseholdId == householdId && t.Id == id
            && writable.Any(a => a.Id == t.SourceAccountId)
            && writable.Any(a => a.Id == t.DestinationAccountId), ct);
    }

    private IQueryable<VisibleRow> Visible(Guid householdId, Guid memberId)
    {
        var visible = VisibleAccounts(householdId, memberId);
        return from t in db.Transfers.AsNoTracking()
               join source in db.Accounts on t.SourceAccountId equals source.Id
               join destination in db.Accounts on t.DestinationAccountId equals destination.Id
               let sourceVisible = visible.Any(a => a.Id == t.SourceAccountId)
               let destinationVisible = visible.Any(a => a.Id == t.DestinationAccountId)
               where t.HouseholdId == householdId && (sourceVisible || destinationVisible)
               select new VisibleRow
               {
                   Transfer = t, Source = source, Destination = destination,
                   SourceVisible = sourceVisible, DestinationVisible = destinationVisible
               };
    }

    private static IQueryable<TransferResult> Project(IQueryable<VisibleRow> rows, Guid memberId)
        => rows.Select(row => new TransferResult(row.Transfer.Id,
            row.SourceVisible ? new TransferSide(row.Source.Id, row.Source.Name,
                row.Transfer.SourceCurrency.Value, row.Transfer.SentAmount) : null,
            row.DestinationVisible ? new TransferSide(row.Destination.Id, row.Destination.Name,
                row.Transfer.DestinationCurrency.Value, row.Transfer.ReceivedAmount) : null,
            row.Transfer.OccurredOn, row.SourceVisible && row.DestinationVisible ? row.Transfer.Note : null,
            row.Transfer.CreatedAt, row.Transfer.UpdatedAt, row.Transfer.VoidedAt != null,
            (row.Source.Scope == AccountScope.Household || row.Source.Owners.Any(o => o.HouseholdMemberId == memberId))
            && (row.Destination.Scope == AccountScope.Household || row.Destination.Owners.Any(o => o.HouseholdMemberId == memberId))));

    public Task<TransferResult?> GetVisibleAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct)
        => Project(Visible(householdId, memberId).Where(row => row.Transfer.Id == id), memberId)
            .SingleOrDefaultAsync(ct);

    private sealed class VisibleRow
    {
        public Transfer Transfer { get; init; } = null!;
        public Account Source { get; init; } = null!;
        public Account Destination { get; init; } = null!;
        public bool SourceVisible { get; init; }
        public bool DestinationVisible { get; init; }
    }

    public async Task<TransferPage> GetPageAsync(Guid householdId, Guid memberId, TransferQuery query, CancellationToken ct)
    {
        // Filter on permitted account IDs before projecting/redacting the counterparty.
        var visibleAccounts = VisibleAccounts(householdId, memberId);
        var ids = db.Transfers.Where(t => t.HouseholdId == householdId);
        if (query.AccountId is not null)
            ids = ids.Where(t => visibleAccounts.Any(a => a.Id == query.AccountId)
                && (t.SourceAccountId == query.AccountId || t.DestinationAccountId == query.AccountId));
        if (query.From is not null) ids = ids.Where(t => t.OccurredOn >= query.From);
        if (query.To is not null) ids = ids.Where(t => t.OccurredOn <= query.To);
        if (query.CursorDate is not null && query.CursorCreatedAt is not null && query.CursorId is not null)
            ids = ids.Where(t => t.OccurredOn < query.CursorDate
                || (t.OccurredOn == query.CursorDate && (t.CreatedAt < query.CursorCreatedAt
                    || (t.CreatedAt == query.CursorCreatedAt && t.Id.CompareTo(query.CursorId.Value) < 0))));
        var rows = Visible(householdId, memberId).Where(row => ids.Any(x => x.Id == row.Transfer.Id))
            .OrderByDescending(row => row.Transfer.OccurredOn)
            .ThenByDescending(row => row.Transfer.CreatedAt).ThenByDescending(row => row.Transfer.Id)
            .Take(query.PageSize + 1);
        var page = await Project(rows, memberId).ToListAsync(ct);
        var hasMore = page.Count > query.PageSize;
        if (hasMore) page.RemoveAt(page.Count - 1);
        return new TransferPage(page, hasMore);
    }
}
