using Averio.Application.Transactions.Refunds;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
namespace Averio.Infrastructure.Persistence.Repositories;
internal sealed class ItemRefundRepository(AverioDbContext db) : IItemRefundRepository
{
    public void Add(ItemRefund refund) => db.Set<ItemRefund>().Add(refund);
    public Task<ItemRefund?> ByOperationAsync(Guid h, Guid operation, CancellationToken ct)
        => db.Set<ItemRefund>().SingleOrDefaultAsync(x => x.HouseholdId == h && x.OperationId == operation, ct);
    public Task<ItemRefund?> GetAsync(Guid h, Guid id, CancellationToken ct)
        => db.Set<ItemRefund>().SingleOrDefaultAsync(x => x.HouseholdId == h && x.Id == id, ct);
    public Task<ItemRefund?> ByCreditAsync(Guid h, Guid creditId, CancellationToken ct)
        => db.Set<ItemRefund>().SingleOrDefaultAsync(x => x.HouseholdId == h && x.CreditTransactionId == creditId, ct);
    public async Task<IReadOnlyList<ItemRefund>> ListAsync(Guid h, Guid transaction, CancellationToken ct)
        => await db.Set<ItemRefund>().Where(x => x.HouseholdId == h && x.TransactionId == transaction).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
    public async Task<bool> CreditHasDependenciesAsync(Guid h, Guid transaction, CancellationToken ct)
        => await db.PersonalMoneyEntries.AnyAsync(x => x.HouseholdId == h && (x.TransactionId == transaction || x.RelatedTransactionId == transaction), ct)
        || await db.Set<ItemRefund>().AnyAsync(x => x.HouseholdId == h && x.CreditTransactionId == transaction, ct)
        || await db.Set<Averio.Domain.Recurring.RecurringOccurrence>().AnyAsync(x => x.HouseholdId == h && x.TransactionId == transaction, ct)
        || await db.Set<Averio.Domain.Receipts.ReceiptScan>().AnyAsync(x => x.HouseholdId == h && x.PostedTransactionId == transaction, ct)
        || await db.Set<Averio.Domain.Recurring.ReimbursementClaim>().AnyAsync(x => x.HouseholdId == h && x.TransactionId == transaction, ct);
}
