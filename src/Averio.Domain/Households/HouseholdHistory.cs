namespace Averio.Domain.Households;

// A user's immutable, privacy-filtered view at departure. It is never a live household session.
public sealed class HouseholdHistory
{
    private HouseholdHistory() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid UserId { get; private set; }
    public string HouseholdName { get; private set; } = null!;
    public DateTimeOffset LeftAtUtc { get; private set; }
    public bool WasLastMember { get; private set; }
    public string SnapshotJson { get; private set; } = null!;
    public static HouseholdHistory Create(Guid householdId, Guid userId, string name,
        DateTimeOffset now, bool wasLastMember, string snapshotJson) => new()
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, UserId = userId,
            HouseholdName = name, LeftAtUtc = now, WasLastMember = wasLastMember,
            SnapshotJson = snapshotJson
        };
}
