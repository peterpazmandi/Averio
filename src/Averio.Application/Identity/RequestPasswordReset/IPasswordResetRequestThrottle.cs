namespace Averio.Application.Identity.RequestPasswordReset;

/// <summary>
/// Per-account cooldown guarding how often a password reset email may be
/// dispatched, independent of any per-IP request rate limiting.
/// </summary>
public interface IPasswordResetRequestThrottle
{
    /// <summary>
    /// Returns <see langword="true"/> and reserves the cooldown window if a
    /// reset email may be sent for <paramref name="email"/> right now.
    /// </summary>
    bool TryAcquire(string email);
}
