namespace Averio.Application.Identity;

public interface IIdentityService
{
    Task<IdentityUserInfo?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<IdentityUserInfo?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<IdentityUserCreationResult> CreateUserAsync(string email, string password, string preferredCulture, CancellationToken cancellationToken);
    Task<string?> GenerateEmailVerificationTokenAsync(Guid userId, CancellationToken cancellationToken);
    Task<EmailConfirmationStatus> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken);
    Task<bool> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken);
    Task<string?> GeneratePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken);

    Task<PasswordResetStatus> ResetPasswordAsync(Guid userId, string token, string password, CancellationToken cancellationToken);
}

public interface IUserSessionService
{
    Task<SignInStatus> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken);

    Task SignOutAsync(CancellationToken cancellationToken);
    Task<bool> CompleteTwoFactorSignInAsync(string code, bool rememberMe, CancellationToken cancellationToken);
    Task<bool> CompleteRecoveryCodeSignInAsync(string code, bool rememberMe, CancellationToken cancellationToken);
}

public interface IEmailVerificationSender
{
    Task<bool> TrySendAsync(Guid userId, string email, string verificationToken, string culture, CancellationToken cancellationToken);
}
public interface IPasswordResetSender
{
    Task<bool> TrySendAsync(Guid userId, string email, string resetToken, string culture, CancellationToken cancellationToken);
}

public sealed record IdentityUserInfo(
    Guid Id,
    string Email,
    string? DisplayName,
    bool EmailConfirmed,
    string PreferredCulture);

public sealed record IdentityUserCreationResult(bool Succeeded, Guid? UserId, IReadOnlyCollection<string> ErrorCodes);

public enum EmailConfirmationStatus
{
    Succeeded,
    AlreadyConfirmed,
    UserNotFound,
    InvalidToken
}

public enum SignInStatus
{
    Succeeded,
    RequiresTwoFactor,
    InvalidCredentials,
    EmailNotConfirmed,
    LockedOut
}

public static class IdentityCreationErrorCodes
{
    public const string DuplicateEmail = "duplicate_email";

    public const string Rejected = "identity_rejected";
}
public enum PasswordResetStatus
{
    Succeeded,
    UserNotFound,
    InvalidToken,
    Rejected
}
