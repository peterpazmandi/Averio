namespace Averio.Application.Identity.ResendVerification;

/// <summary>
/// Per-account cooldown guarding how often an email verification resend may
/// be dispatched, independent of any per-IP request rate limiting.
/// </summary>
public interface IResendEmailVerificationThrottle
{
    /// <summary>
    /// Returns <see langword="true"/> and reserves the cooldown window if a
    /// verification email may be sent for <paramref name="email"/> right now.
    /// </summary>
    bool TryAcquire(string email);
}
