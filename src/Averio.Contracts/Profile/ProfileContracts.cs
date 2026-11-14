namespace Averio.Contracts.Profile;

public sealed record ProfileResponse(Guid UserId, string DisplayName, string Email, bool EmailVerified,
    string PreferredCulture, bool TwoFactorEnabled, bool HasProfilePicture);
public sealed record UpdateProfileRequest(string? DisplayName, string? PreferredCulture);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record PasswordConfirmationRequest(string? Password);
public sealed record AuthenticatorSetupResponse(string SharedKey, string AuthenticatorUri);
public sealed record EnableAuthenticatorRequest(string? Code);
public sealed record DisableAuthenticatorRequest(string? Password, string? Code);
public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
