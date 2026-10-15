namespace Averio.Domain.Recurring;

public sealed class RecurringOccurrence
{
    private RecurringOccurrence() { }
    public Guid HouseholdId { get; private set; }
    public Guid RuleId { get; private set; }
    public DateOnly DueOn { get; private set; }
    public Guid? TransactionId { get; private set; }
    public decimal? Amount { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
    public static RecurringOccurrence Create(RecurringRule rule, DateOnly dueOn, Guid? transactionId, decimal? amount, DateTimeOffset now)
        => new() { HouseholdId = rule.HouseholdId, RuleId = rule.Id, DueOn = dueOn,
            TransactionId = transactionId, Amount = amount, ProcessedAt = now };
}
