namespace Averio.Domain.Recurring;

public sealed record RecurringItemDraft(string Description, Guid CategoryId, decimal Amount);

public sealed class RecurringItem
{
    private RecurringItem() { }
    public Guid Id { get; private set; }
    public Guid RuleId { get; private set; }
    public int Position { get; private set; }
    public string Description { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public decimal Amount { get; private set; }
    internal static RecurringItem Create(Guid ruleId, RecurringItemDraft item, int position) => new()
    {
        Id = Guid.NewGuid(), RuleId = ruleId, Position = position,
        Description = item.Description.Trim(), CategoryId = item.CategoryId, Amount = item.Amount
    };
}
public sealed class RecurringLabel
{
    private RecurringLabel() { }
    public Guid HouseholdId { get; private set; }
    public Guid RuleId { get; private set; }
    public Guid LabelId { get; private set; }
    internal static RecurringLabel Create(Guid householdId, Guid ruleId, Guid labelId)
        => new() { HouseholdId = householdId, RuleId = ruleId, LabelId = labelId };
}
