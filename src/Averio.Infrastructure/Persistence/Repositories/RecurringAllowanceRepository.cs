using Averio.Application.PersonalMoney;
using Averio.Domain.PersonalMoney;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class RecurringAllowanceRepository(AverioDbContext db) : IRecurringAllowanceRepository
{
    public async Task<IReadOnlyList<RecurringAllowanceRule>> ListAsync(Guid householdId, CancellationToken ct)
        => await db.RecurringAllowanceRules.AsNoTracking().Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.IsStopped).ThenBy(x => x.NextDueOn).ThenBy(x => x.CreatedAt).ToArrayAsync(ct);

    public Task<RecurringAllowanceRule?> GetAsync(Guid householdId, Guid ruleId, CancellationToken ct)
        => db.RecurringAllowanceRules.SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Id == ruleId, ct);

    public async Task<IReadOnlyList<PersonalMoneyEntry>> HistoryAsync(Guid householdId, Guid ruleId, CancellationToken ct)
        => await db.PersonalMoneyEntries.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.RecurringAllowanceRuleId == ruleId)
            .OrderByDescending(x => x.OccurredOn).Take(100).ToArrayAsync(ct);

    public Task<bool> HasReceiptAsync(Guid ruleId, DateOnly dueOn, CancellationToken ct)
        => db.PersonalMoneyEntries.AnyAsync(x => x.RecurringAllowanceRuleId == ruleId
            && x.OccurredOn == dueOn, ct);

    public async Task<IReadOnlyList<RecurringAllowanceCandidate>> DueAsync(DateOnly today, int limit,
        CancellationToken ct, RecurringAllowanceCursor? after = null)
    {
        var afterDate = after?.DueOn;
        var afterId = after?.RuleId ?? Guid.Empty;
        return await (from rule in db.RecurringAllowanceRules.AsNoTracking()
            join member in db.HouseholdMembers.AsNoTracking()
                on new { rule.HouseholdId, Id = rule.RecipientMemberId }
                equals new { member.HouseholdId, member.Id }
            join household in db.Households.AsNoTracking()
                on rule.HouseholdId equals household.Id
            where !rule.IsPaused && !rule.IsStopped && !rule.IsBlocked
                && rule.NextDueOn <= today && member.LeftAtUtc == null
                && household.ArchivedAtUtc == null && household.FinancialSetupCompletedAtUtc != null
                && (afterDate == null || rule.NextDueOn > afterDate
                    || (rule.NextDueOn == afterDate && rule.Id.CompareTo(afterId) > 0))
            orderby rule.NextDueOn, rule.Id
            select new RecurringAllowanceCandidate(rule.Id, rule.HouseholdId,
                rule.RecipientMemberId, member.UserId, rule.Version, rule.NextDueOn!.Value))
            .Take(limit).ToArrayAsync(ct);
    }
    public void Add(RecurringAllowanceRule rule) => db.RecurringAllowanceRules.Add(rule);
}
