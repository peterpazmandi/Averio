namespace Averio.Application.Identity.RequestPasswordReset;

/// <summary>
/// Hands a password reset email off for asynchronous delivery so the
/// request path never awaits the outbound send. This prevents the send
/// latency from acting as an account-enumeration timing oracle.
/// </summary>
public interface IPasswordResetEmailQueue
{
    void Enqueue(PasswordResetEmailJob job);
}

public sealed record PasswordResetEmailJob(
    Guid UserId,
    string Email,
    string ResetToken,
    string Culture);
