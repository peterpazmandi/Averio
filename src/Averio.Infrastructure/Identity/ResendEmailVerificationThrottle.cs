using Averio.Application.Identity.ResendVerification;
using Microsoft.Extensions.Caching.Memory;

namespace Averio.Infrastructure.Identity;

internal sealed class ResendEmailVerificationThrottle(IMemoryCache cache) : IResendEmailVerificationThrottle
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    public bool TryAcquire(string email)
    {
        var key = $"email-verification-cooldown:{email.Trim().ToUpperInvariant()}";

        if (cache.TryGetValue(key, out _))
        {
            return false;
        }

        cache.Set(key, true, Cooldown);

        return true;
    }
}
