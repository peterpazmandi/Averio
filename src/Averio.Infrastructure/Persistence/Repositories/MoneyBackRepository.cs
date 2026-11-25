using Averio.Application.Recurring;
using Averio.Domain.Accounts;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class MoneyBackRepository(AverioDbContext db) : IMoneyBackRepository
{
    public async Task<IReadOnlyList<MoneyBackRow>> ListAsync(Guid householdId, Guid memberId, int offset, int take, string? status, CancellationToken ct)
    {
        var visibleTransactions = db.VisibleTransactions(householdId, memberId).AsNoTracking();
        var claims = ReimbursementRepository.Visible(db, householdId, memberId).AsNoTracking();
        var refunds = db.Set<ItemRefund>().AsNoTracking().Where(r => r.HouseholdId == householdId);
        if (status is not null)
        {
            if (!Enum.TryParse<ReimbursementStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
                return [];
            claims = claims.Where(c => c.Status == parsed);
            refunds = parsed switch
            {
                ReimbursementStatus.Received => refunds.Where(r => r.CancelledAt == null),
                ReimbursementStatus.Cancelled => refunds.Where(r => r.CancelledAt != null),
                _ => refunds.Where(_ => false),
            };
        }

        var claimKeys = claims.Select(c => new { c.Id, Date = c.PaymentOn, Kind = 0 });
        var refundKeys = refunds.Where(r => visibleTransactions.Any(t => t.Id == r.TransactionId)
                && visibleTransactions.Any(t => t.Id == r.CreditTransactionId))
            .Select(r => new { r.Id, Date = r.ReceivedOn, Kind = 1 });
        var keys = await claimKeys.Concat(refundKeys).OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ThenBy(x => x.Kind)
            .Skip(offset).Take(take).ToArrayAsync(ct);
        var claimIds = keys.Where(x => x.Kind == 0).Select(x => x.Id).ToArray();
        var refundIds = keys.Where(x => x.Kind == 1).Select(x => x.Id).ToArray();

        var claimRows = from claim in claims.Where(c => claimIds.Contains(c.Id))
            join source in visibleTransactions on claim.TransactionId equals source.Id
            join account in db.Accounts.AsNoTracking() on source.AccountId equals account.Id
            let received = db.Set<ReimbursementPayment>().Where(p => p.HouseholdId == householdId && p.ClaimId == claim.Id && p.UnlinkedAt == null)
                .Sum(p => (decimal?)p.Amount) ?? 0m
            select new Projection
            {
                Kind = "Reimbursement", Id = claim.Id, Description = source.Description, Provider = claim.Provider,
                Merchant = null, Currency = claim.Currency, Amount = claim.PotentialAmount,
                Outstanding = source.Status != TransactionStatus.Posted || source.Kind != TransactionKind.Expense
                    || (claim.Status != ReimbursementStatus.Submitted && claim.Status != ReimbursementStatus.PartiallyReceived && claim.Status != ReimbursementStatus.Received)
                    ? 0m : claim.PotentialAmount > received ? claim.PotentialAmount - received : 0m,
                Date = claim.PaymentOn, Status = claim.Status.ToString(), SourceId = claim.TransactionId, CreditId = null,
                CanModify = account.CutoverAtUtc == null && (account.Scope == AccountScope.Household || account.Owners.Any(o => o.HouseholdMemberId == memberId)),
            };
        var refundRows = from refund in refunds.Where(r => refundIds.Contains(r.Id))
            join source in visibleTransactions on refund.TransactionId equals source.Id
            join credit in visibleTransactions on refund.CreditTransactionId equals credit.Id
            join account in db.Accounts.AsNoTracking() on source.AccountId equals account.Id
            select new Projection
            {
                Kind = "Refund", Id = refund.Id, Description = credit.Description, Provider = null,
                Merchant = credit.Merchant, Currency = credit.Currency.Value, Amount = refund.Amount, Outstanding = null,
                Date = refund.ReceivedOn, Status = refund.CancelledAt == null ? "Received" : "Cancelled",
                SourceId = refund.TransactionId, CreditId = refund.CreditTransactionId,
                CanModify = refund.CancelledAt == null && account.CutoverAtUtc == null
                    && (account.Scope == AccountScope.Household || account.Owners.Any(o => o.HouseholdMemberId == memberId)),
            };
        var claimDetails = await claimRows.ToArrayAsync(ct);
        var refundDetails = await refundRows.ToArrayAsync(ct);
        var details = claimDetails.Concat(refundDetails).ToDictionary(x => (x.Kind, x.Id));
        return keys.Select(k => details.GetValueOrDefault((k.Kind == 0 ? "Reimbursement" : "Refund", k.Id)))
            .Where(x => x is not null)
            .Select(x => new MoneyBackRow(x!.Kind, x.Id, x.Description, x.Provider, x.Merchant, x.Currency,
                x.Amount, x.Outstanding, x.Date, x.Status, x.SourceId, x.CreditId, x.CanModify)).ToArray();
    }

    private sealed class Projection
    {
        public string Kind { get; init; } = null!;
        public Guid Id { get; init; }
        public string Description { get; init; } = null!;
        public string? Provider { get; init; }
        public string? Merchant { get; init; }
        public string Currency { get; init; } = null!;
        public decimal Amount { get; init; }
        public decimal? Outstanding { get; init; }
        public DateOnly Date { get; init; }
        public string Status { get; init; } = null!;
        public Guid? SourceId { get; init; }
        public Guid? CreditId { get; init; }
        public bool CanModify { get; init; }
    }
}
