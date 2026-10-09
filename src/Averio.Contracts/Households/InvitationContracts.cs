namespace Averio.Contracts.Households;

public sealed record CreateInvitationRequest(string Email);
public sealed record InvitationVersionRequest(Guid ExpectedVersion);
public sealed record InvitationTokenRequest(string? Token, Guid? InvitationId = null);
public sealed record AccountCutoverRequest(Guid AccountId, string ExpectedVersion, string OpeningBalance, string Handling);
public sealed record AcceptInvitationRequest(string? Token, Guid? InvitationId, Guid ExpectedSourceVersion,
    bool Confirmed, Guid? SuccessorMemberId, IReadOnlyList<AccountCutoverRequest> Accounts);
public sealed record InvitationResponse(Guid Id, string Email, string State, string DeliveryStatus,
    Guid Version, DateTimeOffset ExpiresAt, DateTimeOffset LastSentAt);
public sealed record InvitationPreviewResponse(string HouseholdName, bool AlreadyJoined, bool CanJoin,
    DeparturePreviewResponse? Departure);
public sealed record AcceptInvitationResponse(Guid HouseholdId);

public sealed record DepartureAccountResponse(Guid Id, string Name, string Currency, string Type, string Version, string Balance);
public sealed record DepartureMemberResponse(Guid Id, string? DisplayName);
public sealed record DeparturePreviewResponse(Guid HouseholdId, string? HouseholdName, Guid Version, bool WillArchive,
    bool RequiresSuccessor, int AccountsStaying, IReadOnlyList<DepartureAccountResponse> Accounts, IReadOnlyList<DepartureMemberResponse> Successors);
