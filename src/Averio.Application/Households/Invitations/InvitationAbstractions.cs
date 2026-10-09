using Averio.Domain.Households;

namespace Averio.Application.Households.Invitations;

public interface IInvitationRepository
{
    Task LockUserAsync(Guid userId, CancellationToken ct);
    Task<Household?> LockHouseholdAsync(Guid householdId, CancellationToken ct);
    Task<HouseholdInvitation?> FindAsync(Guid id, CancellationToken ct);
    Task<HouseholdInvitation?> FindByHashAsync(string hash, CancellationToken ct);
    Task<IReadOnlyList<HouseholdInvitation>> ListAsync(Guid householdId, CancellationToken ct);
    Task<IReadOnlyList<PendingInvitation>> PendingAsync(string normalizedEmail, CancellationToken ct);
    void Add(HouseholdInvitation invitation);
    Task<DeparturePreview> DepartureAsync(Household source, Guid userId, DateTimeOffset now, CancellationToken ct);
    Task JoinAsync(Household source, Household target, Guid userId, InvitationAcceptance input, DateTimeOffset now, CancellationToken ct);
    Task RecordDeliveryAsync(Guid id, Guid version, bool sent, CancellationToken ct);
}
public interface IInvitationEmailSender
{
    Task<bool> SendAsync(string email, string token, string householdName, CancellationToken ct);
}
public sealed record InvitationInput(string Email);
public sealed record InvitationView(Guid Id, string Email, string State, string DeliveryStatus,
    Guid Version, DateTimeOffset ExpiresAt, DateTimeOffset LastSentAt);
public sealed record PendingInvitation(Guid Id, string HouseholdName);
public sealed record DepartureAccount(Guid Id, string Name, string Currency, string Type, string Version, decimal Balance);
public sealed record DepartureMember(Guid Id, string? DisplayName);
public sealed record DeparturePreview(Guid HouseholdId, string? HouseholdName, Guid Version, bool WillArchive,
    bool RequiresSuccessor, int AccountsStaying, IReadOnlyList<DepartureAccount> Accounts, IReadOnlyList<DepartureMember> Successors);
public sealed record InvitationPreview(string HouseholdName, bool AlreadyJoined, bool CanJoin, DeparturePreview? Departure);
public sealed record AccountCutoverChoice(Guid AccountId, string ExpectedVersion, decimal OpeningBalance, string Handling);
public sealed record InvitationAcceptance(string? Token, Guid? InvitationId, Guid? ExpectedSourceVersion,
    bool Confirmed, Guid? SuccessorMemberId, IReadOnlyList<AccountCutoverChoice> Accounts);
