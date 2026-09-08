using Averio.Application.Identity.RequestPasswordReset;
using Microsoft.Extensions.Caching.Memory;

namespace Averio.Infrastructure.Identity;

internal sealed class PasswordResetRequestThrottle(IMemoryCache cache) : IPasswordResetRequestThrottle
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    public bool TryAcquire(string email)
    {
        var key = $"password-reset-cooldown:{email.Trim().ToUpperInvariant()}";

        if (cache.TryGetValue(key, out _))
        {
            return false;
        }

        cache.Set(key, true, Cooldown);

        return true;
    }
}
