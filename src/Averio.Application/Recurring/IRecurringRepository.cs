using Averio.Domain.Recurring;
namespace Averio.Application.Recurring;

public sealed record RecurringCandidate(Guid RuleId, Guid HouseholdId, Guid UserId, Guid Version, DateOnly DueOn = default);
public sealed record RecurringCursor(DateOnly DueOn, Guid RuleId);
public interface IRecurringRepository
{
    Task<IReadOnlyList<RecurringRule>> ListAsync(Guid householdId, Guid memberId, CancellationToken ct);
    Task<RecurringRule?> GetAsync(Guid householdId, Guid memberId, Guid ruleId, CancellationToken ct);
    Task<RecurringOccurrence?> OccurrenceAsync(Guid ruleId, DateOnly dueOn, CancellationToken ct);
    Task<IReadOnlyList<RecurringOccurrence>> HistoryAsync(Guid householdId, Guid memberId, Guid ruleId, CancellationToken ct);
    Task<IReadOnlyList<RecurringCandidate>> DueAsync(DateOnly today, int limit, CancellationToken ct, RecurringCursor? after = null);
    void Add(RecurringRule rule);
    void Add(RecurringOccurrence occurrence);
}
