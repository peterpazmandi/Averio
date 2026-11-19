using Averio.Application.Recurring;
using Averio.Domain.Recurring;
using Averio.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
namespace Averio.Infrastructure.Persistence.Repositories;
internal sealed class ReimbursementRepository(AverioDbContext db) : IReimbursementRepository
{
    // Claims are personal to the recurring-rule creator and additionally respect both accounts' visibility.
    private IQueryable<ReimbursementClaim> Visible(Guid householdId, Guid memberId) => db.Set<ReimbursementClaim>()
        .Where(c => c.HouseholdId == householdId && c.HouseholdMemberId == memberId
            && db.VisibleTransactions(householdId, memberId).Any(t => t.Id == c.TransactionId)
            && db.Accounts.Any(a => a.HouseholdId == householdId && a.Id == c.DestinationAccountId
                && (a.Visibility == AccountVisibility.Household || a.Owners.Any(o => o.HouseholdMemberId == memberId))));
    public async Task<IReadOnlyList<ReimbursementClaim>> ListAsync(Guid householdId, Guid memberId, Guid? transactionId, CancellationToken ct)
        => await Visible(householdId, memberId).AsNoTracking().Include(c => c.Events)
            .Where(c => transactionId == null ? c.Status != ReimbursementStatus.Cancelled : c.TransactionId == transactionId)
            .OrderBy(c => c.CreatedAt).ToArrayAsync(ct);
    public Task<ReimbursementClaim?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct)
        => Visible(householdId, memberId).Include(c => c.Events).SingleOrDefaultAsync(c => c.Id == id, ct);
    public Task<ReimbursementClaim?> FindForTransactionAsync(Guid householdId, Guid transactionId, CancellationToken ct)
        => db.Set<ReimbursementClaim>().SingleOrDefaultAsync(c => c.HouseholdId == householdId && c.TransactionId == transactionId, ct);
    public async Task<IReadOnlyDictionary<Guid, string>> StatusesAsync(Guid householdId, Guid memberId, IReadOnlyList<Guid> transactionIds, CancellationToken ct)
    {
        if (transactionIds.Count == 0) return new Dictionary<Guid, string>();
        var rows = await Visible(householdId, memberId).AsNoTracking().Where(c => transactionIds.Contains(c.TransactionId))
            .Select(c => new { c.TransactionId, c.Status }).ToArrayAsync(ct);
        return rows.ToDictionary(c => c.TransactionId, c => c.Status.ToString());
    }
    public void Add(ReimbursementClaim claim) => db.Add(claim);
}
