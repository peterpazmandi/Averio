using Averio.Application.Recurring;
using Averio.Domain.Recurring;
using Microsoft.EntityFrameworkCore;
namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class RecurringRepository(AverioDbContext db) : IRecurringRepository
{
    public async Task<IReadOnlyList<RecurringRule>> ListAsync(Guid householdId, Guid memberId, CancellationToken ct)
        => await db.Set<RecurringRule>().AsNoTracking().Where(x => x.HouseholdId == householdId && x.HouseholdMemberId == memberId)
            .OrderBy(x => x.IsStopped).ThenBy(x => x.NextDueOn).ThenBy(x => x.Name).ToArrayAsync(ct);
    public Task<RecurringRule?> GetAsync(Guid householdId, Guid memberId, Guid ruleId, CancellationToken ct)
        => db.Set<RecurringRule>().SingleOrDefaultAsync(x => x.Id == ruleId && x.HouseholdId == householdId && x.HouseholdMemberId == memberId, ct);
    public Task<RecurringOccurrence?> OccurrenceAsync(Guid ruleId, DateOnly dueOn, CancellationToken ct)
        => db.Set<RecurringOccurrence>().AsNoTracking().SingleOrDefaultAsync(x => x.RuleId == ruleId && x.DueOn == dueOn, ct);
    public async Task<IReadOnlyList<RecurringOccurrence>> HistoryAsync(Guid householdId, Guid memberId, Guid ruleId, CancellationToken ct)
        => await db.Set<RecurringOccurrence>().AsNoTracking().Where(x => x.RuleId == ruleId && x.HouseholdId == householdId && (x.TransactionId == null || db.VisibleTransactions(householdId, memberId).Any(t => t.Id == x.TransactionId))).OrderByDescending(x => x.DueOn).Take(24).ToArrayAsync(ct);
    public async Task<IReadOnlyList<RecurringCandidate>> DueAsync(DateOnly today, int limit, CancellationToken ct)
        => await (from rule in db.Set<RecurringRule>().AsNoTracking()
            join member in db.HouseholdMembers.AsNoTracking() on new { rule.HouseholdId, Id = rule.HouseholdMemberId } equals new { member.HouseholdId, member.Id }
            where rule.Automatic && !rule.IsPaused && !rule.IsStopped && !rule.IsBlocked && rule.NextDueOn <= today
                && member.LeftAtUtc == null
            orderby rule.NextDueOn, rule.Id
            select new RecurringCandidate(rule.Id, rule.HouseholdId, member.UserId, rule.Version)).Take(limit).ToArrayAsync(ct);
    public void Add(RecurringRule rule) => db.Add(rule);
    public void Add(RecurringOccurrence occurrence) => db.Add(occurrence);
}
