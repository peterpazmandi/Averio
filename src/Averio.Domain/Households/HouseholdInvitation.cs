namespace Averio.Domain.Households;

public sealed class HouseholdInvitation
{
    private HouseholdInvitation() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid InviterUserId { get; private set; }
    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public string State { get; private set; } = "Pending";
    public string DeliveryStatus { get; private set; } = "Unknown";
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSentAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public int SendCount { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public Guid? SourceHouseholdId { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static HouseholdInvitation Create(Guid householdId, Guid inviter, string email, string normalizedEmail,
        string hash, DateTimeOffset now)
    {
        var invitation = new HouseholdInvitation { Id = Guid.NewGuid(), HouseholdId = householdId,
            InviterUserId = inviter, Email = email, NormalizedEmail = normalizedEmail, CreatedAt = now };
        invitation.Rotate(hash, now);
        return invitation;
    }
    public bool IsPending(DateTimeOffset now) => State == "Pending" && ExpiresAt > now;
    public void Rotate(string hash, DateTimeOffset now)
    {
        if (State != "Pending") throw new InvalidOperationException("Invitation is no longer pending.");
        TokenHash = hash; LastSentAt = now; ExpiresAt = now.AddDays(7);
        SendCount++; DeliveryStatus = "Unknown"; Version = Guid.NewGuid();
    }
    public void Revoke(DateTimeOffset now)
    {
        if (State != "Pending") throw new InvalidOperationException("Invitation is no longer pending.");
        State = "Revoked"; RevokedAt = now; Version = Guid.NewGuid();
    }
    public void Accept(Guid userId, Guid sourceHouseholdId, DateTimeOffset now)
    {
        if (!IsPending(now)) throw new InvalidOperationException("Invitation is no longer valid.");
        State = "Accepted"; AcceptedByUserId = userId; SourceHouseholdId = sourceHouseholdId;
        AcceptedAt = now; Version = Guid.NewGuid();
    }
}
