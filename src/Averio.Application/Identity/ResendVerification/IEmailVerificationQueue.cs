namespace Averio.Application.Identity.ResendVerification;

/// <summary>
/// Hands an email verification resend off for asynchronous delivery so the
/// request path never awaits the outbound send. This prevents the send
/// latency from acting as an account-enumeration timing oracle.
/// </summary>
public interface IEmailVerificationQueue
{
    void Enqueue(EmailVerificationJob job);
}

public sealed record EmailVerificationJob(
    Guid UserId,
    string Email,
    string VerificationToken,
    string Culture);
