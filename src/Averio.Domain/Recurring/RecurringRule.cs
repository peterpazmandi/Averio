using Averio.Domain.Transactions;
namespace Averio.Domain.Recurring;

public enum RecurringFrequency { Weekly, Monthly, Yearly }

public sealed class RecurringRule
{
    private RecurringRule() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid HouseholdMemberId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public TransactionKind Kind { get; private set; }
    public TransactionVisibility Visibility { get; private set; }
    public RecurringFrequency Frequency { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public int OccurrenceIndex { get; private set; }
    public DateOnly? NextDueOn { get; private set; }
    public bool Automatic { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsStopped { get; private set; }
    public bool IsBlocked { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static RecurringRule Create(Guid household, Guid member, Guid account, Guid category,
        string name, decimal amount, TransactionKind kind, TransactionVisibility visibility,
        RecurringFrequency frequency, DateOnly startsOn, DateOnly? endsOn, bool automatic, DateTimeOffset now)
    {
        if (household == Guid.Empty || member == Guid.Empty || account == Guid.Empty || category == Guid.Empty
            || !Enum.IsDefined(frequency) || !Enum.IsDefined(kind) || !Enum.IsDefined(visibility)
            || startsOn == default) throw new ArgumentException("Invalid recurring rule.");
        var r = new RecurringRule { Id = Guid.NewGuid(), HouseholdId = household, HouseholdMemberId = member,
            AccountId = account, Kind = kind, Visibility = visibility, Frequency = frequency,
            StartsOn = startsOn, CreatedAt = now };
        r.Update(category, name, amount, visibility, endsOn, automatic, false, false, now);
        return r;
    }
    public void Update(Guid category, string name, decimal amount, TransactionVisibility visibility,
        DateOnly? endsOn, bool automatic, bool paused, bool stopped, DateTimeOffset now)
    {
        if (category == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100
            || amount <= 0 || amount > 999999999999999.9999m || decimal.Round(amount, 4) != amount
            || !Enum.IsDefined(visibility) || endsOn < StartsOn || IsStopped)
            throw new ArgumentException("Invalid recurring rule update.");
        CategoryId = category; Name = name.Trim(); Amount = amount; Visibility = visibility;
        EndsOn = endsOn; Automatic = automatic; IsPaused = paused; IsStopped = stopped; IsBlocked = false;
        NextDueOn = DueDate(StartsOn, Frequency, OccurrenceIndex);
        if (NextDueOn > EndsOn || IsStopped) NextDueOn = null;
        Touch(now);
    }
    public void Advance(DateTimeOffset now)
    {
        OccurrenceIndex++;
        NextDueOn = DueDate(StartsOn, Frequency, OccurrenceIndex);
        if (NextDueOn > EndsOn || IsStopped) NextDueOn = null;
        IsBlocked = false; Touch(now);
    }
    public void Block(DateTimeOffset now) { IsBlocked = true; Touch(now); }
    private void Touch(DateTimeOffset now) { Version = Guid.NewGuid(); UpdatedAt = now; }
    public static DateOnly? DueDate(DateOnly anchor, RecurringFrequency frequency, int index)
    {
        if (index < 0 || !Enum.IsDefined(frequency)) throw new ArgumentOutOfRangeException(nameof(index));
        try { return frequency switch {
            RecurringFrequency.Weekly => anchor.AddDays(checked(index * 7)),
            RecurringFrequency.Monthly => anchor.AddMonths(index),
            RecurringFrequency.Yearly => anchor.AddYears(index),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency)) }; }
        catch (ArgumentOutOfRangeException) { return null; }
        catch (OverflowException) { return null; }
    }
}
