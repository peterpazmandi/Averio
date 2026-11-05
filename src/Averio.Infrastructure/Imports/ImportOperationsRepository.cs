using Averio.Application.Imports;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Activities;
using Averio.Domain.Imports;
using Averio.Domain.PersonalMoney;
using Averio.Domain.Transactions;
using Averio.Domain.Transfers;
using Averio.Domain.Accounts;
using Averio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Imports;
internal sealed partial class ImportRepository
{
    public async Task<IReadOnlyList<ImportBatch>> RecentMappedAsync(Guid householdId, Guid memberId, CancellationToken ct)
        => await db.Set<ImportBatch>().AsNoTracking().Where(b => b.HouseholdId == householdId && b.MemberId == memberId && b.CompletedAt != null && b.RecordLinks != null)
            .OrderByDescending(b => b.CompletedAt).ThenByDescending(b => b.Id).Take(20).ToArrayAsync(ct);

    public async Task<IReadOnlyList<ImportExistingRecord>> FindOverlapAsync(Guid householdId, Guid memberId, Guid[] accountIds, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var transactions = await db.VisibleTransactions(householdId, memberId).AsNoTracking()
            .Where(t => accountIds.Contains(t.AccountId) && t.Status == TransactionStatus.Posted && t.OccurredOn >= start && t.OccurredOn <= end)
            .Select(t => new ImportExistingRecord(t.Id, t.Kind.ToString(), t.AccountId, null, t.OccurredOn, t.Amount, null, t.Description, t.UpdatedAt))
            .Take(100001).ToArrayAsync(ct);
        var visible = db.Accounts.Where(a => a.HouseholdId == householdId && (a.Visibility == AccountVisibility.Household || a.Owners.Any(o => o.HouseholdMemberId == memberId)));
        var transfers = await (from t in db.Transfers.AsNoTracking()
            join a in visible on t.SourceAccountId equals a.Id
            join b in visible on t.DestinationAccountId equals b.Id
            where t.HouseholdId == householdId && accountIds.Contains(t.SourceAccountId) && accountIds.Contains(t.DestinationAccountId)
                && t.VoidedAt == null && t.OccurredOn >= start && t.OccurredOn <= end
            select new ImportExistingRecord(t.Id, "Transfer", t.SourceAccountId, t.DestinationAccountId, t.OccurredOn, t.SentAmount, t.ReceivedAmount, t.Note ?? "", t.UpdatedAt))
            .Take(100001).ToArrayAsync(ct);
        if (transactions.Length > 100000 || transfers.Length > 100000) throw new BusinessRuleException("import_review_too_large", "Error_ImportReviewTooLarge");
        return transactions.Concat(transfers).ToArray();
    }
    public async Task<IReadOnlySet<Guid>> LinkedRecordsAsync(Guid householdId, Guid[] ids, CancellationToken ct)
    {
        // Check all household dependencies without revealing their private descriptions or owners.
        var activities = await db.Set<ActivityAssignment>().Where(a => a.HouseholdId == householdId && ids.Contains(a.TransactionId) && a.ActivityId != null)
            .Select(a => a.TransactionId).ToArrayAsync(ct);
        var money = await db.Set<PersonalMoneyEntry>().Where(e => e.HouseholdId == householdId &&
            (ids.Contains(e.TransactionId ?? Guid.Empty) || ids.Contains(e.RelatedTransactionId ?? Guid.Empty) || ids.Contains(e.TransferId ?? Guid.Empty)))
            .Select(e => new { e.TransactionId, e.RelatedTransactionId, e.TransferId }).ToArrayAsync(ct);
        var receipts = await db.Set<Averio.Domain.Receipts.ReceiptScan>().Where(x => x.HouseholdId == householdId && ids.Contains(x.PostedTransactionId ?? Guid.Empty)).Select(x => x.PostedTransactionId!.Value).ToArrayAsync(ct);
        var recurring = await db.Set<Averio.Domain.Recurring.RecurringOccurrence>().Where(x => x.HouseholdId == householdId && ids.Contains(x.TransactionId ?? Guid.Empty)).Select(x => x.TransactionId!.Value).ToArrayAsync(ct);
        return activities.Concat(receipts).Concat(recurring).Concat(money.SelectMany(e => new[] { e.TransactionId, e.RelatedTransactionId, e.TransferId }).OfType<Guid>()).ToHashSet();
    }
    public async Task VoidRecordsAsync(Guid householdId, Guid[] transactionIds, Guid[] transferIds, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Undo requires a household-locked transaction.");
        var transactions = await db.Transactions.Where(t => t.HouseholdId == householdId && transactionIds.Contains(t.Id)).ToArrayAsync(ct);
        var transfers = await db.Transfers.Where(t => t.HouseholdId == householdId && transferIds.Contains(t.Id)).ToArrayAsync(ct);
        await ApplyAggregatesAsync(transactionIds, ct, -1);
        foreach (var transaction in transactions) transaction.Void(now);
        foreach (var transfer in transfers) transfer.Void(now);
    }
}