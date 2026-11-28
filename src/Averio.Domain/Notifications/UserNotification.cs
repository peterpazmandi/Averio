namespace Averio.Domain.Notifications;

// No financial or free-form user content is stored in the inbox.
public sealed class UserNotification
{
    private UserNotification() { }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid HouseholdMemberId { get; private set; }
    public string Type { get; private set; } = null!;
    public Guid TargetId { get; private set; }
    public string OccurrenceKey { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset? DismissedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static UserNotification Create(Guid user, Guid household, Guid member, string type,
        Guid target, string occurrenceKey, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(), RecipientUserId = user, HouseholdId = household,
            HouseholdMemberId = member, Type = type, TargetId = target,
            OccurrenceKey = occurrenceKey, CreatedAt = now
        };

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
    public void Dismiss(DateTimeOffset now) { MarkRead(now); DismissedAt ??= now; }
    public void Resolve(DateTimeOffset now) => ResolvedAt ??= now;
}
