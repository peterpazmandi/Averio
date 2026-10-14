using Averio.Application.Transactions.SavedViews;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class SavedViewRepository(AverioDbContext db) : ISavedViewRepository
{
    public async Task LockMemberAsync(Guid householdId, Guid memberId, CancellationToken ct)
        => await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM household_members WHERE household_id = {householdId} AND id = {memberId} FOR UPDATE", ct);
    public async Task<IReadOnlyList<SavedTransactionView>> ListAsync(Guid householdId, Guid memberId, CancellationToken ct)
        => await db.Set<SavedTransactionView>().AsNoTracking().Where(x => x.HouseholdId == householdId && x.HouseholdMemberId == memberId).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
    public Task<SavedTransactionView?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct)
        => db.Set<SavedTransactionView>().SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.HouseholdMemberId == memberId && x.Id == id, ct);
    public void Add(SavedTransactionView view) => db.Add(view);
    public void Remove(SavedTransactionView view) => db.Remove(view);
}
