namespace Averio.Application.Common.Exceptions;

public abstract class AverioException : Exception
{
    protected AverioException(
        string code,
        string resourceKey,
        params object[] arguments)
        : base(resourceKey)
    {
        Code = code;
        ResourceKey = resourceKey;
        Arguments = arguments;
    }

    public string Code { get; }

    public string ResourceKey { get; }

    public object[] Arguments { get; }
}

public sealed class NotFoundException : AverioException
{
    public NotFoundException(
        string resourceName,
        object identifier)
        : base(
            "resource_not_found",
            ErrorResourceKeys.NotFound,
            resourceName,
            identifier)
    {
    }
}

public sealed class ConflictException : AverioException
{
    public ConflictException(
        string code,
        string resourceKey,
        params object[] arguments)
        : base(
            code,
            resourceKey,
            arguments)
    {
    }
}

public sealed class BusinessRuleException : AverioException
{
    public BusinessRuleException(
        string code,
        string resourceKey,
        params object[] arguments)
        : base(
            code,
            resourceKey,
            arguments)
    {
    }
}

public sealed class AuthenticationFailedException
    : AverioException
{
    public AuthenticationFailedException()
        : base(
            "invalid_credentials",
            ErrorResourceKeys.InvalidCredentials)
    {
    }
}

public sealed class EmailNotVerifiedException
    : AverioException
{
    public EmailNotVerifiedException()
        : base(
            "email_not_verified",
            ErrorResourceKeys.EmailNotVerified)
    {
    }
}

public sealed class AccountLockedException
    : AverioException
{
    public AccountLockedException()
        : base(
            "account_locked",
            ErrorResourceKeys.AccountLocked)
    {
    }
}

public sealed class InvalidEmailVerificationTokenException
    : AverioException
{
    public InvalidEmailVerificationTokenException()
        : base(
            "invalid_email_verification_token",
            ErrorResourceKeys.InvalidEmailVerificationToken)
    {
    }
}

public sealed class HouseholdRequiredException
    : AverioException
{
    public HouseholdRequiredException()
        : base(
            "household_required",
            ErrorResourceKeys.HouseholdRequired)
    {
    }
}

public sealed class OnboardingRequiredException : AverioException
{
    public OnboardingRequiredException() : base(
            "onboarding_required",
            ErrorResourceKeys.OnboardingRequired)
    {
    }
}

public sealed class InvalidPasswordResetTokenException  : AverioException
{
    public InvalidPasswordResetTokenException() : base(
        "invalid_password_reset_token",
        ErrorResourceKeys.InvalidPasswordResetToken) {}
}

public sealed class ForbiddenException : AverioException
{
    public ForbiddenException() : base(
            "operation_forbidden",
            ErrorResourceKeys.Forbidden)
    {
    }
}

public static class ErrorResourceKeys
{
    public const string NotFound = "Error_NotFound";
    public const string BusinessRule = "Error_BusinessRule";
    public const string InvalidCredentials = "Error_InvalidCredentials";
    public const string EmailNotVerified = "Error_EmailNotVerified";
    public const string Forbidden = "Error_OperationForbidden";
    public const string AccountLocked = "Error_AccountLocked";
    public const string InvalidEmailVerificationToken = "Error_InvalidEmailVerificationToken";
    public const string EmailAlreadyRegistered = "Error_EmailAlreadyRegistered";
    public const string HouseholdRequired = "Error_HouseholdRequired";
    public const string IdentityCreationFailed = "Error_IdentityCreationFailed";
    public const string OnboardingRequired = "Error_OnboardingRequired";
    public const string InvalidPasswordResetToken = "Error_InvalidPasswordResetToken";
    public const string PasswordRejected = "Error_PasswordRejected";
}