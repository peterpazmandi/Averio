using System.Globalization;
namespace Averio.Domain.Recurring;

public sealed class RecurringOccurrence
{
    private RecurringOccurrence() { }
    public Guid HouseholdId { get; private set; }
    public Guid RuleId { get; private set; }
    public DateOnly DueOn { get; private set; }
    public Guid? TransactionId { get; private set; }
    public decimal? Amount { get; private set; }
    public string? ItemAmountsSnapshot { get; private set; }
    public bool MatchesItemAmounts(IReadOnlyList<decimal> amounts) => ItemAmountsSnapshot is not null && ItemAmountsSnapshot == Snapshot(amounts);
    private static string Snapshot(IEnumerable<decimal> amounts) => string.Join(";", amounts.Select(x => x.ToString("G29", CultureInfo.InvariantCulture)));
    public DateTimeOffset ProcessedAt { get; private set; }
    public static RecurringOccurrence Create(RecurringRule rule, DateOnly dueOn, Guid? transactionId, decimal? amount, DateTimeOffset now, IReadOnlyList<decimal>? itemAmounts = null)
        => new() { HouseholdId = rule.HouseholdId, RuleId = rule.Id, DueOn = dueOn,
            TransactionId = transactionId, Amount = amount, ProcessedAt = now, ItemAmountsSnapshot = itemAmounts is null ? null : Snapshot(itemAmounts) };
}
