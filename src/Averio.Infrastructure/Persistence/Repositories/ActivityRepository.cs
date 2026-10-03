using Averio.Application.Activities;
using Averio.Domain.Activities;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class ActivityRepository(AverioDbContext db) : IActivityRepository
{
    public async Task LockMemberAsync(Guid householdId, Guid memberId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("An activity write requires a transaction.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM household_members WHERE household_id = {householdId} AND id = {memberId} FOR UPDATE", ct);
    }
    public Task<Activity?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct)
        => db.Set<Activity>().SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.HouseholdMemberId == memberId && x.Id == id, ct);

    public async Task<IReadOnlyList<ActivityResult>> ListAsync(Guid householdId, Guid memberId, DateOnly through, CancellationToken ct)
    {
        var activities = await db.Set<Activity>().AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.HouseholdMemberId == memberId)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
        var totals = await (
            from link in db.Set<ActivityAssignment>().AsNoTracking()
            join transaction in db.VisibleTransactions(householdId, memberId).AsNoTracking() on link.TransactionId equals transaction.Id
            where link.HouseholdId == householdId && link.HouseholdMemberId == memberId && link.ActivityId != null
                && transaction.Status == TransactionStatus.Posted && transaction.OccurredOn <= through
            group transaction by new { link.ActivityId, transaction.Currency } into g
            select new { g.Key.ActivityId, g.Key.Currency,
                Income = g.Sum(x => x.Kind == TransactionKind.Income ? x.Amount : 0m),
                Expenses = g.Sum(x => x.Kind == TransactionKind.Expense ? x.Amount : 0m), Count = g.LongCount() }).ToListAsync(ct);
        var grouped = totals.GroupBy(x => x.ActivityId!.Value).ToDictionary(g => g.Key,
            g => (IReadOnlyList<ActivityCurrencyResult>)g.OrderBy(x => x.Currency.Value)
                .Select(x => new ActivityCurrencyResult(x.Currency.Value, x.Income, x.Expenses, x.Count)).ToArray());
        return activities.Select(x => new ActivityResult(x, grouped.GetValueOrDefault(x.Id, []))).ToArray();
    }
    public Task<ActivityAssignment?> GetAssignmentAsync(Guid householdId, Guid memberId, Guid transactionId, CancellationToken ct)
        => db.Set<ActivityAssignment>().SingleOrDefaultAsync(x => x.HouseholdId == householdId
            && x.HouseholdMemberId == memberId && x.TransactionId == transactionId, ct);
    public async Task<ActivityAssignmentResult?> ReadAssignmentAsync(Guid householdId, Guid memberId, Guid transactionId, CancellationToken ct)
        => await (from link in db.Set<ActivityAssignment>().AsNoTracking()
            join activity in db.Set<Activity>() on link.ActivityId equals activity.Id into matches
            from activity in matches.DefaultIfEmpty()
            where link.HouseholdId == householdId && link.HouseholdMemberId == memberId && link.TransactionId == transactionId
            select new ActivityAssignmentResult(link.ActivityId, activity == null ? null : activity.Name, link.Version)).SingleOrDefaultAsync(ct);
    public void Add(Activity activity) => db.Set<Activity>().Add(activity);
    public void Add(ActivityAssignment assignment) => db.Set<ActivityAssignment>().Add(assignment);
}
