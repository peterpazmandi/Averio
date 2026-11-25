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
        => db.Set<ItemRefund>().Where(x => x.HouseholdId == h && x.CreditTransactionId == creditId)
            .OrderBy(x => x.CancelledAt != null).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<ItemRefund>> ListAsync(Guid h, Guid member, Guid transaction, CancellationToken ct)
        => await VisibleForTransaction(db, h, member, transaction).ToListAsync(ct);
    internal static IQueryable<ItemRefund> VisibleForTransaction(AverioDbContext db, Guid h, Guid member, Guid transaction)
        => db.Set<ItemRefund>().Where(x => x.HouseholdId == h && x.TransactionId == transaction
            && db.VisibleTransactions(h, member).Any(t => t.Id == x.CreditTransactionId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id);
    public Task<bool> CreditHasDependenciesAsync(Guid h, Guid transaction, CancellationToken ct)
        => RefundCreditEligibility.WithDependencies(db, h).AnyAsync(t => t.Id == transaction, ct);
}
