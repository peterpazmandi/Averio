using Averio.Application.Recurring;
using Averio.Domain.Recurring;
using Averio.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
namespace Averio.Infrastructure.Persistence.Repositories;
internal sealed class ReimbursementRepository(AverioDbContext db) : IReimbursementRepository
{
    // Claims are personal to the recurring-rule creator and additionally respect both accounts' visibility.
    internal static IQueryable<ReimbursementClaim> Visible(AverioDbContext db, Guid householdId, Guid memberId) => db.Set<ReimbursementClaim>()
        .Where(c => c.HouseholdId == householdId && c.HouseholdMemberId == memberId
            && db.VisibleTransactions(householdId, memberId).Any(t => t.Id == c.TransactionId)
            && !c.Payments.Any(p => !db.VisibleTransactions(householdId, memberId).Any(t => t.Id == p.CreditTransactionId))
            && db.Accounts.Any(a => a.HouseholdId == householdId && a.Id == c.DestinationAccountId
                && (a.Visibility == AccountVisibility.Household || a.Owners.Any(o => o.HouseholdMemberId == memberId))));
    private IQueryable<ReimbursementClaim> Visible(Guid householdId, Guid memberId) => Visible(db, householdId, memberId);
    public async Task<IReadOnlyList<ReimbursementClaim>> ListAsync(Guid householdId, Guid memberId, Guid? transactionId, CancellationToken ct, bool includeCancelled = false)
        => await Visible(householdId, memberId).AsNoTracking().Include(c => c.Events).Include(c => c.Payments).AsSingleQuery()
            .Where(c => transactionId == null
                ? includeCancelled || c.Status != ReimbursementStatus.Cancelled
                : c.TransactionId == transactionId)
            .OrderBy(c => c.CreatedAt).ToArrayAsync(ct);
    public Task<ReimbursementClaim?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct)
        => Visible(householdId, memberId).Include(c => c.Events).Include(c => c.Payments).AsSingleQuery().SingleOrDefaultAsync(c => c.Id == id, ct);
    public Task<ReimbursementClaim?> FindForTransactionAsync(Guid householdId, Guid transactionId, CancellationToken ct)
        => db.Set<ReimbursementClaim>().SingleOrDefaultAsync(c => c.HouseholdId == householdId && c.TransactionId == transactionId, ct);
    public async Task<IReadOnlyDictionary<Guid, string>> StatusesAsync(Guid householdId, Guid memberId, IReadOnlyList<Guid> transactionIds, CancellationToken ct)
    {
        if (transactionIds.Count == 0) return new Dictionary<Guid, string>();
        var rows = await Visible(householdId, memberId).AsNoTracking().Where(c => transactionIds.Contains(c.TransactionId))
            .Select(c => new { c.TransactionId, c.Status }).ToArrayAsync(ct);
        return rows.ToDictionary(c => c.TransactionId, c => c.Status.ToString());
    }
    public async Task<IReadOnlyList<decimal>> ActiveCreditAllocationsAsync(Guid householdId, Guid creditId, CancellationToken ct)
        => await db.Set<ReimbursementPayment>().Where(p => p.HouseholdId == householdId && p.CreditTransactionId == creditId && p.UnlinkedAt == null)
            .Select(p => p.Amount).ToArrayAsync(ct);
    public Task<ReimbursementPayment?> PaymentByOperationAsync(Guid h, Guid operation, CancellationToken ct)
        => db.Set<ReimbursementPayment>().SingleOrDefaultAsync(p => p.HouseholdId == h && p.OperationId == operation, ct);
    public async Task<IReadOnlySet<Guid>> PaymentProtectedIdsAsync(Guid householdId, IReadOnlyList<Guid> transactionIds, CancellationToken ct)
    {
        if (transactionIds.Count == 0) return new HashSet<Guid>();
        return (await db.Transactions.Where(t => t.HouseholdId == householdId && transactionIds.Contains(t.Id)
            && db.Set<ReimbursementPayment>().Any(p => p.HouseholdId == householdId && p.UnlinkedAt == null
                && (p.CreditTransactionId == t.Id || db.Set<ReimbursementClaim>().Any(c => c.Id == p.ClaimId && c.TransactionId == t.Id))))
            .Select(t => t.Id).ToArrayAsync(ct)).ToHashSet();
    }
    public Task<bool> HasActivePaymentAsync(Guid h, Guid transactionId, CancellationToken ct)
        => db.Set<ReimbursementPayment>().AnyAsync(p => p.HouseholdId == h && p.UnlinkedAt == null
            && (p.CreditTransactionId == transactionId || db.Set<ReimbursementClaim>().Any(c => c.Id == p.ClaimId && c.TransactionId == transactionId)), ct);
    public async Task<IReadOnlyList<ReimbursementCreditCandidate>> PaymentCandidatesAsync(Guid h, Guid member, ReimbursementClaim claim, decimal available, string? search, int offset, DateOnly today, CancellationToken ct)
    {
        var q = db.VisibleTransactions(h, member).AsNoTracking().Where(t => t.AccountId == claim.DestinationAccountId
            && t.ReimbursementPrivacyEligible && t.CreatedByHouseholdMemberId == member
            && t.Kind == Averio.Domain.Transactions.TransactionKind.Income && t.Status == Averio.Domain.Transactions.TransactionStatus.Posted
            && !t.IsArchived && t.OccurredOn >= claim.PaymentOn && t.OccurredOn <= today && available > 0
            && t.Amount > (db.Set<ReimbursementPayment>().Where(p => p.HouseholdId == h && p.CreditTransactionId == t.Id && p.UnlinkedAt == null)
                .Sum(p => (decimal?)p.Amount) ?? 0m)
            && !db.Set<ReimbursementPayment>().Any(p => p.HouseholdId == h && p.ClaimId == claim.Id && p.CreditTransactionId == t.Id && p.UnlinkedAt == null)
            && !db.Set<Averio.Domain.Transactions.ItemRefund>().Any(p => p.HouseholdId == h && p.CreditTransactionId == t.Id && p.CancelledAt == null)
            && !db.Set<Averio.Domain.Recurring.RecurringOccurrence>().Any(p => p.TransactionId == t.Id)
            && !db.Set<Averio.Domain.Receipts.ReceiptScan>().Any(p => p.PostedTransactionId == t.Id)
            && !db.Set<ReimbursementClaim>().Any(c => c.TransactionId == t.Id)
            && !db.PersonalMoneyEntries.Where(p => p.TransactionId == t.Id || p.RelatedTransactionId == t.Id)
                .GroupBy(p => new { p.WalletId, p.Kind, p.OccurredOn, p.TransactionId, p.RelatedTransactionId, p.TransactionItemId })
                .Any(g => g.Sum(p => p.Amount) != 0));
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(t => t.Description.Contains(search) || (t.Merchant != null && t.Merchant.Contains(search)) || (t.Note != null && t.Note.Contains(search)));
        var rows = await q.OrderByDescending(t => t.OccurredOn).ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Skip(offset).Take(21).Select(t => new { Credit = t,
                Allocated = db.Set<ReimbursementPayment>().Where(p => p.HouseholdId == h && p.CreditTransactionId == t.Id && p.UnlinkedAt == null)
                    .Sum(p => (decimal?)p.Amount) ?? 0m }).ToArrayAsync(ct);
        return rows.Select(r => new ReimbursementCreditCandidate(r.Credit, r.Credit.Amount - r.Allocated,
            Math.Min(available, r.Credit.Amount - r.Allocated))).ToArray();
    }
    public async Task<IReadOnlyList<ReimbursementDestinationRecovery>> DestinationRecoveryAsync(Guid h, Guid member, CancellationToken ct)
        => await DestinationRecoveryQuery(db, h, member).ToArrayAsync(ct);
    internal static IQueryable<ReimbursementDestinationRecovery> DestinationRecoveryQuery(AverioDbContext db, Guid h, Guid member)
        => db.Set<ReimbursementClaim>().AsNoTracking()
            .Where(c => c.HouseholdId == h && c.HouseholdMemberId == member && c.Status != ReimbursementStatus.Cancelled
                && !c.Payments.Any(p => p.UnlinkedAt == null)
                && !db.Accounts.Any(a => a.HouseholdId == h && a.Id == c.DestinationAccountId
                    && (a.Visibility == AccountVisibility.Household || a.Owners.Any(o => o.HouseholdMemberId == member)))
                && db.VisibleTransactions(h, member).Any(t => t.Id == c.TransactionId
                    && db.Accounts.Any(a => a.HouseholdId == h && a.Id == t.AccountId && a.CutoverAtUtc == null
                        && (a.Scope == AccountScope.Household || a.Owners.Any(o => o.HouseholdMemberId == member)))))
            .OrderBy(c => c.Id).Take(100).Select(c => new ReimbursementDestinationRecovery(c.Id, c.Version));
    public Task<ReimbursementClaim?> GetOwnedAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct)
        => db.Set<ReimbursementClaim>().Include(c => c.Events).Include(c => c.Payments).AsSingleQuery()
            .SingleOrDefaultAsync(c => c.HouseholdId == householdId && c.HouseholdMemberId == memberId && c.Id == id, ct);
    public async Task<IReadOnlyList<ReimbursementRecoveryLink>> RecoveryLinksAsync(Guid householdId, Guid memberId, CancellationToken ct)
    {
        var visible = Visible(householdId, memberId);
        return await db.Set<ReimbursementClaim>()
            .Where(c => c.HouseholdId == householdId && c.HouseholdMemberId == memberId
                && (!visible.Any(v => v.Id == c.Id)
                    || !db.Transactions.Any(t => t.Id == c.TransactionId && db.Accounts.Any(a => a.Id == t.AccountId
                        && a.CutoverAtUtc == null && (a.Scope == AccountScope.Household || a.Owners.Any(o => o.HouseholdMemberId == memberId))))))
            .SelectMany(c => c.Payments.Where(p => p.UnlinkedAt == null).Select(p => new { ClaimId = c.Id, PaymentId = p.Id }))
            .OrderBy(p => p.ClaimId).ThenBy(p => p.PaymentId).Take(100).Select(p => new ReimbursementRecoveryLink(p.ClaimId, p.PaymentId)).ToArrayAsync(ct);
    }
    public void Add(ReimbursementClaim claim) => db.Add(claim);
}

