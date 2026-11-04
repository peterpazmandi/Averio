using Averio.Application.Imports;
using Averio.Domain.Accounts;
using Averio.Domain.Imports;
using Averio.Domain.Transactions;
using Averio.Domain.Transfers;
using Averio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Imports;

internal sealed partial class ImportRepository
{
    public async Task<IReadOnlyList<ImportHistoryStored>> ListCompletedAsync(Guid householdId, Guid memberId, int page, CancellationToken ct)
        => await db.Set<ImportBatch>().AsNoTracking()
            .Where(b => b.HouseholdId == householdId && b.MemberId == memberId && b.CompletedAt != null)
            .OrderByDescending(b => b.CompletedAt).ThenByDescending(b => b.Id).Skip(page * 10).Take(11)
            .Select(b => new ImportHistoryStored(b.Id, b.SourceName, b.CompletedAt!.Value, b.Summary,
                b.Summary == null ? b.Payload : null, b.RecordLinks != null)).ToArrayAsync(ct);

    public async Task<IReadOnlyList<ImportCurrentRecord>> GetCurrentRecordsAsync(Guid householdId, Guid memberId,
        Guid[] transactionIds, Guid[] transferIds, CancellationToken ct)
    {
        var transactions = await (from t in db.VisibleTransactions(householdId, memberId).AsNoTracking()
            join a in db.Accounts on t.AccountId equals a.Id
            where transactionIds.Contains(t.Id)
            select new ImportCurrentRecord(t.Id, false, t.UpdatedAt, t.Status == TransactionStatus.Voided,
                a.Status == AccountStatus.Active && a.CutoverAtUtc == null &&
                (a.Scope == AccountScope.Household || a.Owners.Any(o => o.HouseholdMemberId == memberId))))
            .ToArrayAsync(ct);
        var visible = db.Accounts.Where(a => a.HouseholdId == householdId &&
            (a.Visibility == AccountVisibility.Household || a.Owners.Any(o => o.HouseholdMemberId == memberId)));
        var transfers = await (from t in db.Set<Transfer>().AsNoTracking()
            join a in visible on t.SourceAccountId equals a.Id
            join b in visible on t.DestinationAccountId equals b.Id
            where t.HouseholdId == householdId && transferIds.Contains(t.Id)
            select new ImportCurrentRecord(t.Id, true, t.UpdatedAt, t.VoidedAt != null,
                a.Status == AccountStatus.Active && b.Status == AccountStatus.Active && a.CutoverAtUtc == null && b.CutoverAtUtc == null &&
                (a.Scope == AccountScope.Household || a.Owners.Any(o => o.HouseholdMemberId == memberId)) &&
                (b.Scope == AccountScope.Household || b.Owners.Any(o => o.HouseholdMemberId == memberId))))
            .ToArrayAsync(ct);
        return transactions.Concat(transfers).ToArray();
    }
}
