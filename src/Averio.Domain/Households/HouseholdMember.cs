namespace Averio.Domain.Households;

public sealed class HouseholdMember
{
    private HouseholdMember()
    {
    }

    private HouseholdMember(
        Guid id,
        Guid householdId,
        Guid userId,
        HouseholdRole role,
        DateTimeOffset joinedAtUtc)
    {
        Id = id;
        HouseholdId = householdId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid HouseholdId { get; private set; }

    public Guid UserId { get; private set; }

    public HouseholdRole Role { get; private set; }

    public DateTimeOffset JoinedAtUtc { get; private set; }
    internal static HouseholdMember CreateMember(Guid householdId, Guid userId, DateTimeOffset now)
        => new(Guid.NewGuid(), householdId, userId, HouseholdRole.Member, now);

    internal static HouseholdMember CreateOwner(
        Guid householdId,
        Guid userId,
        DateTimeOffset joinedAtUtc)
    {
        return new HouseholdMember(
            Guid.NewGuid(),
            householdId,
            userId,
            HouseholdRole.Owner,
            joinedAtUtc);
    }
}
