namespace Averio.Domain.Activities;

public sealed class Activity
{
    private Activity() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid HouseholdMemberId { get; private set; }
    public string Name { get; private set; } = null!;
    public DateOnly? StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Activity Create(Guid householdId, Guid memberId, string name,
        DateOnly? startsOn, DateOnly? endsOn, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || memberId == Guid.Empty)
            throw new ArgumentException("Household and membership are required.");
        var activity = new Activity { Id = Guid.NewGuid(), HouseholdId = householdId,
            HouseholdMemberId = memberId, CreatedAt = Truncate(now), UpdatedAt = Truncate(now) };
        activity.SetDetails(name, startsOn, endsOn);
        return activity;
    }

    public void Update(string name, DateOnly? startsOn, DateOnly? endsOn, bool isClosed, DateTimeOffset now)
    {
        SetDetails(name, startsOn, endsOn);
        IsClosed = isClosed;
        UpdatedAt = new(Math.Max(Truncate(now).UtcTicks, UpdatedAt.UtcTicks + 10), TimeSpan.Zero);
    }

    private void SetDetails(string name, DateOnly? startsOn, DateOnly? endsOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Trim().Length > 100) throw new ArgumentOutOfRangeException(nameof(name));
        if (startsOn > endsOn) throw new ArgumentException("End date must follow start date.");
        Name = name.Trim(); StartsOn = startsOn; EndsOn = endsOn;
    }
    private static DateTimeOffset Truncate(DateTimeOffset now)
        => new(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
}
