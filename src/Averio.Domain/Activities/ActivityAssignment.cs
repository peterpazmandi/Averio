namespace Averio.Domain.Activities;

// Keep unassigned rows: an old browser must not overwrite a later assignment.
public sealed class ActivityAssignment
{
    private ActivityAssignment() { }
    public Guid HouseholdId { get; private set; }
    public Guid HouseholdMemberId { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid? ActivityId { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ActivityAssignment Create(Guid householdId, Guid memberId, Guid transactionId)
    {
        if (householdId == Guid.Empty || memberId == Guid.Empty || transactionId == Guid.Empty)
            throw new ArgumentException("Assignment identifiers are required.");
        return new() { HouseholdId = householdId, HouseholdMemberId = memberId, TransactionId = transactionId };
    }
    public void Assign(Activity? activity, DateTimeOffset now)
    {
        if (activity is not null && (activity.HouseholdId != HouseholdId || activity.HouseholdMemberId != HouseholdMemberId))
            throw new InvalidOperationException("An activity must belong to the same membership.");
        if (activity?.Id == ActivityId) return;
        if (activity?.IsClosed == true) throw new InvalidOperationException("Reopen before assigning transactions.");
        ActivityId = activity?.Id;
        Version = Guid.NewGuid();
        UpdatedAt = new(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
    }
}
