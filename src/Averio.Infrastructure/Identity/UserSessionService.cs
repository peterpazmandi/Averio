using Averio.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Averio.Infrastructure.Identity;

internal sealed class UserSessionService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TimeProvider timeProvider,
    ILogger<UserSessionService> logger)
    : IUserSessionService
{
    public async Task<SignInStatus> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email.Trim());

        if (user is null)
            return SignInStatus.InvalidCredentials;

        if (await userManager.IsLockedOutAsync(user))
            return SignInStatus.LockedOut;

        // Check password FIRST so an attacker cannot learn that
        // an unverified email exists using the wrong password.
        var passwordValid = await userManager.CheckPasswordAsync(user, password);

        if (!passwordValid)
        {
            if (await userManager.GetLockoutEnabledAsync(user))
            {
                await userManager.AccessFailedAsync(user);

                if (await userManager.IsLockedOutAsync(user))
                    return SignInStatus.LockedOut;
            }

            return SignInStatus.InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        if (!user.EmailConfirmed)
            return SignInStatus.EmailNotConfirmed;

        user.LastLoginAtUtc = timeProvider.GetUtcNow();

        var updateResult = await userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            logger.LogWarning(
                "Could not update last login timestamp for user {UserId}",
                user.Id);
        }

        await signInManager.SignInAsync(user, isPersistent: rememberMe);

        return SignInStatus.Succeeded;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await signInManager.SignOutAsync();
    }
}