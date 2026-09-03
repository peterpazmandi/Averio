using Averio.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Averio.Infrastructure.Identity;

internal sealed class IdentityService(UserManager<ApplicationUser> userManager, TimeProvider timeProvider) : IIdentityService
{
    public async Task<IdentityUserInfo?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());
        return user is null ? null : Map(user);
    }

    public async Task<IdentityUserInfo?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : Map(user);
    }

    public async Task<IdentityUserCreationResult> CreateUserAsync(
        string email,
        string password,
        string preferredCulture,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = email.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = false,
            LockoutEnabled = true,
            PreferredCulture = preferredCulture,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        try
        {
            var result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                return new IdentityUserCreationResult(true, user.Id, []);
            }

            var errorCodes = result.Errors
                .Select(MapIdentityError)
                .Distinct()
                .ToArray();

            return new IdentityUserCreationResult(false, null, errorCodes);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            return new IdentityUserCreationResult(
                false,
                null,
                [IdentityCreationErrorCodes.DuplicateEmail]);
        }
    }

    public async Task<string?> GenerateEmailVerificationTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;
        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task<EmailConfirmationStatus> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return EmailConfirmationStatus.UserNotFound;

        if (user.EmailConfirmed)
            return EmailConfirmationStatus.AlreadyConfirmed;

        var result = await userManager.ConfirmEmailAsync(user, token);

        return result.Succeeded
            ? EmailConfirmationStatus.Succeeded
            : EmailConfirmationStatus.InvalidToken;
    }

    private static IdentityUserInfo Map(ApplicationUser user)
        => new(user.Id, user.Email!, user.EmailConfirmed, user.PreferredCulture);

    private static string MapIdentityError(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateEmail" => IdentityCreationErrorCodes.DuplicateEmail,
            "DuplicateUserName" => IdentityCreationErrorCodes.DuplicateEmail,
            _ => IdentityCreationErrorCodes.Rejected
        };
    }
}