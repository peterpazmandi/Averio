using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Averio.Infrastructure.Identity;

internal sealed class PasswordResetTokenProvider<TUser>(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<TUser>> logger)
    : DataProtectorTokenProvider<TUser>(dataProtectionProvider, options, logger) where TUser : class;

internal sealed class PasswordResetTokenProviderOptions
    : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        Name = "AverioPasswordResetTokenProvider";
        TokenLifespan = TimeSpan.FromHours(3);
    }
}