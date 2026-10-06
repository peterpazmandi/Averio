using Averio.Domain.Households;

namespace Averio.Application.Households.Invitations;

public interface IInvitationRepository
{
    Task LockUserAsync(Guid userId, CancellationToken ct);
    Task<Household?> LockHouseholdAsync(Guid householdId, CancellationToken ct);
    Task<HouseholdInvitation?> FindAsync(Guid id, CancellationToken ct);
    Task<HouseholdInvitation?> FindByHashAsync(string hash, CancellationToken ct);
    Task<IReadOnlyList<HouseholdInvitation>> ListAsync(Guid householdId, CancellationToken ct);
    void Add(HouseholdInvitation invitation);
    Task<bool> IsEmptySoloAsync(Household household, Guid userId, CancellationToken ct);
    Task JoinAsync(Household source, Household target, Guid userId, DateTimeOffset now, CancellationToken ct);
    Task RecordDeliveryAsync(Guid id, Guid version, bool sent, CancellationToken ct);
}
public interface IInvitationEmailSender
{
    Task<bool> SendAsync(string email, string token, CancellationToken ct);
}
public sealed record InvitationInput(string Email);
public sealed record InvitationView(Guid Id, string Email, string State, string DeliveryStatus,
    Guid Version, DateTimeOffset ExpiresAt, DateTimeOffset LastSentAt);
public sealed record InvitationPreview(string HouseholdName, bool AlreadyJoined, bool CanJoin);
