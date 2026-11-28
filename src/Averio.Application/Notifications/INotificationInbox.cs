namespace Averio.Application.Notifications;

public sealed record NotificationItem(Guid Id, Guid HouseholdId, string Type, Guid TargetId,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public interface INotificationInbox
{
    Task<IReadOnlyList<NotificationItem>> ListAsync(Guid userId, Guid householdId, bool unreadOnly, CancellationToken ct);
    Task<int> UnreadCountAsync(Guid userId, Guid householdId, CancellationToken ct);
    Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken ct);
    Task<int> MarkAllReadAsync(Guid userId, Guid householdId, CancellationToken ct);
    Task<bool> DismissAsync(Guid userId, Guid id, CancellationToken ct);
    Task ReconcileAllAsync(CancellationToken ct);
}
