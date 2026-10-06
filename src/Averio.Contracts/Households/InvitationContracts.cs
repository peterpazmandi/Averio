namespace Averio.Contracts.Households;
public sealed record CreateInvitationRequest(string Email);
public sealed record InvitationVersionRequest(Guid ExpectedVersion);
public sealed record InvitationTokenRequest(string Token);
public sealed record InvitationResponse(Guid Id, string Email, string State, string DeliveryStatus,
    Guid Version, DateTimeOffset ExpiresAt, DateTimeOffset LastSentAt);
public sealed record InvitationPreviewResponse(string HouseholdName, bool AlreadyJoined, bool CanJoin);
public sealed record AcceptInvitationResponse(Guid HouseholdId);
