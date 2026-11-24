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
    public async Task<IReadOnlyList<ItemRefund>> ListAsync(Guid h, Guid transaction, CancellationToken ct)
        => await db.Set<ItemRefund>().Where(x => x.HouseholdId == h && x.TransactionId == transaction).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
    public async Task<IReadOnlyList<ItemRefund>> ListVisibleAsync(Guid h, Guid member, int skip, int take, CancellationToken ct)
        => await db.Set<ItemRefund>().AsNoTracking().Where(x => x.HouseholdId == h
            && db.VisibleTransactions(h, member).Any(t => t.Id == x.TransactionId)
            && db.VisibleTransactions(h, member).Any(t => t.Id == x.CreditTransactionId))
            .OrderByDescending(x => x.ReceivedOn).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 100)).ToArrayAsync(ct);
    public Task<bool> CreditHasDependenciesAsync(Guid h, Guid transaction, CancellationToken ct)
        => RefundCreditEligibility.WithDependencies(db, h).AnyAsync(t => t.Id == transaction, ct);
}
