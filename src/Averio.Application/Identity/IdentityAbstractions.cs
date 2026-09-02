namespace Averio.Application.Identity;

public interface IIdentityService
{
    Task<IdentityUserInfo?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<IdentityUserInfo?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IdentityUserCreationResult> CreateUserAsync(string email, string password, string preferredCulture, CancellationToken cancellationToken);

    Task<string?> GenerateEmailVerificationTokenAsync(Guid userId, CancellationToken cancellationToken);

    Task<EmailConfirmationStatus> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken);
}

public interface IUserSessionService
{
    Task<SignInStatus> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken);

    Task SignOutAsync(CancellationToken cancellationToken);
}

public interface IEmailVerificationSender
{
    Task<bool> TrySendAsync(Guid userId, string email, string verificationToken, string culture, CancellationToken cancellationToken);
}

public sealed record IdentityUserInfo(Guid Id, string Email, bool EmailConfirmed, string PreferredCulture);

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
    InvalidCredentials,
    EmailNotConfirmed,
    LockedOut
}

public static class IdentityCreationErrorCodes
{
    public const string DuplicateEmail = "duplicate_email";

    public const string Rejected = "identity_rejected";
}