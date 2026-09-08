using FluentValidation;

namespace Averio.Application.Identity.RequestPasswordReset;

public sealed class RequestPasswordResetHandler(
    IValidator<RequestPasswordResetCommand> validator,
    IIdentityService identityService,
    IPasswordResetSender passwordResetSender,
    IPasswordResetRequestThrottle requestThrottle)
{
    public async Task HandleAsync(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await identityService.FindByEmailAsync(command.Email.Trim(), cancellationToken);

        /*
         * Never disclose whether an account exists.
         */
        if (user is null || !user.EmailConfirmed)
        {
            return;
        }

        /*
         * Delivery cooldown: caps re-sends per account regardless of the
         * requester's IP, on top of the per-IP rate limit at the API layer.
         */
        if (!requestThrottle.TryAcquire(user.Email))
        {
            return;
        }

        var token = await identityService.GeneratePasswordResetTokenAsync(user.Id, cancellationToken);

        if (token is null)
        {
            return;
        }

        await passwordResetSender.TrySendAsync(
            user.Id,
            user.Email,
            token,
            user.PreferredCulture,
            cancellationToken);
    }
}