using Averio.Domain.Common;

namespace Averio.Domain.PersonalMoney;

// Monthly entitlement scheduling is intentionally separate from bank recurring transfers.
public sealed class RecurringAllowanceRule
{
    private RecurringAllowanceRule() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid RecipientMemberId { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public int OccurrenceIndex { get; private set; }
    public DateOnly? NextDueOn { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsStopped { get; private set; }
    public bool IsBlocked { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static RecurringAllowanceRule Create(Guid householdId, Guid recipientMemberId,
        CurrencyCode currency, decimal amount, DateOnly startsOn, DateOnly? endsOn, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || recipientMemberId == Guid.Empty
            || !CurrencyCode.IsSupported(currency.Value))
            throw new ArgumentException("A scheduled allowance requires a valid household, member and currency.");
        Validate(amount, startsOn, endsOn);
        return new RecurringAllowanceRule
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, RecipientMemberId = recipientMemberId,
            Currency = currency, Amount = amount, StartsOn = startsOn, EndsOn = endsOn,
            NextDueOn = startsOn, CreatedAt = now, UpdatedAt = now, Version = Guid.NewGuid()
        };
    }

    // Recipient, currency and anchor never change: historical occurrences retain their meaning.
    public void Update(decimal amount, DateOnly? endsOn, bool paused, DateTimeOffset now)
    {
        if (IsStopped) throw new InvalidOperationException("A stopped allowance cannot be restarted.");
        Validate(amount, StartsOn, endsOn);
        if (endsOn is not null && NextDueOn is not null && endsOn < NextDueOn)
            throw new ArgumentException("Use Stop to end a schedule before its next due date.");
        Amount = amount;
        EndsOn = endsOn;
        IsPaused = paused;
        IsBlocked = false;
        Touch(now);
    }

    public void Block(DateTimeOffset now) { IsBlocked = true; Touch(now); }

    public void Stop(DateTimeOffset now)
    {
        IsStopped = true;
        NextDueOn = null;
        Touch(now);
    }

    public void Advance(DateTimeOffset now)
    {
        if (IsStopped || NextDueOn is null) throw new InvalidOperationException("No pending allowance occurrence.");
        OccurrenceIndex = checked(OccurrenceIndex + 1);
        IsBlocked = false;
        NextDueOn = MonthlyDueDate(StartsOn, OccurrenceIndex);
        if (EndsOn is not null && NextDueOn > EndsOn) NextDueOn = null;
        Touch(now);
    }

    public static DateOnly? MonthlyDueDate(DateOnly anchor, int index)
    {
        if (index < 0 || anchor == default) throw new ArgumentOutOfRangeException(nameof(index));
        try { return anchor.AddMonths(index); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static void Validate(decimal amount, DateOnly startsOn, DateOnly? endsOn)
    {
        if (startsOn == default || endsOn < startsOn || amount <= 0
            || amount > 999999999999999.9999m || decimal.Round(amount, 4) != amount)
            throw new ArgumentException("Invalid scheduled allowance amount or dates.");
    }

    private void Touch(DateTimeOffset now) { UpdatedAt = now; Version = Guid.NewGuid(); }
}
