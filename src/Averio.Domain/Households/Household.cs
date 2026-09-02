namespace Averio.Domain.Households;

public sealed class Household
{
    private readonly List<HouseholdMember> _members = [];

    private Household()
    {
    }

    private Household(
        Guid id,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string? Name { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<HouseholdMember> Members =>
        _members;

    public static Household CreateForUser(
        Guid userId,
        DateTimeOffset now)
    {
        var household = new Household(
            Guid.NewGuid(),
            now);

        household._members.Add(
            HouseholdMember.CreateOwner(
                household.Id,
                userId,
                now));

        return household;
    }
}