using Averio.Domain.PersonalMoney;

namespace Averio.Application.PersonalMoney;

public sealed record RecurringAllowanceCandidate(Guid RuleId, Guid HouseholdId,
    Guid RecipientMemberId, Guid Version, DateOnly DueOn);
public sealed record RecurringAllowanceCursor(DateOnly DueOn, Guid RuleId);

public interface IRecurringAllowanceRepository
{
    Task<IReadOnlyList<RecurringAllowanceRule>> ListAsync(Guid householdId, CancellationToken ct);
    Task<RecurringAllowanceRule?> GetAsync(Guid householdId, Guid ruleId, CancellationToken ct);
    Task<IReadOnlyList<PersonalMoneyEntry>> HistoryAsync(Guid householdId, Guid ruleId, CancellationToken ct);
    Task<bool> HasReceiptAsync(Guid ruleId, DateOnly dueOn, CancellationToken ct);
    Task<IReadOnlyList<RecurringAllowanceCandidate>> DueAsync(DateOnly today, int limit,
        CancellationToken ct, RecurringAllowanceCursor? after = null);
    void Add(RecurringAllowanceRule rule);
}
