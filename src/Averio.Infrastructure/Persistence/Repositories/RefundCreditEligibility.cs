using Averio.Domain.Transactions;
using Averio.Domain.Recurring;
using Averio.Domain.Receipts;

namespace Averio.Infrastructure.Persistence.Repositories;

// Shared by candidate filtering and write validation; filter before paging.
internal static class RefundCreditEligibility
{
    internal static IQueryable<Transaction> WithDependencies(AverioDbContext db, Guid householdId)
        => db.Transactions.Where(t => t.HouseholdId == householdId && (
            db.PersonalMoneyEntries.Where(p => p.HouseholdId == householdId && (p.TransactionId == t.Id || p.RelatedTransactionId == t.Id))
                .GroupBy(p => new { p.WalletId, p.Kind, p.OccurredOn, p.TransactionId, p.RelatedTransactionId, p.TransactionItemId })
                .Any(g => g.Sum(p => p.Amount) != 0)
            || db.Set<ReimbursementPayment>().Any(p => p.HouseholdId == householdId && p.CreditTransactionId == t.Id && p.UnlinkedAt == null)
            || db.Set<ItemRefund>().Any(p => p.HouseholdId == householdId && p.CreditTransactionId == t.Id && p.CancelledAt == null)
            || db.Set<RecurringOccurrence>().Any(p => p.HouseholdId == householdId && p.TransactionId == t.Id)
            || db.Set<ReceiptScan>().Any(p => p.HouseholdId == householdId && p.PostedTransactionId == t.Id)
            || db.Set<ReimbursementClaim>().Any(p => p.HouseholdId == householdId && p.TransactionId == t.Id)));
}
