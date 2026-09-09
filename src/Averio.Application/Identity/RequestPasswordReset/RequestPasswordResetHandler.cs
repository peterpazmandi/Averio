using FluentValidation;

namespace Averio.Application.Identity.RequestPasswordReset;

public sealed class RequestPasswordResetHandler(
    IValidator<RequestPasswordResetCommand> validator,
    IIdentityService identityService,
    IPasswordResetEmailQueue passwordResetEmailQueue,
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

        /*
         * Queued, not awaited: delivery latency (SMTP/API round-trip)
         * must never leak into the response time for this endpoint.
         */
        passwordResetEmailQueue.Enqueue(new PasswordResetEmailJob(
            user.Id,
            user.Email,
            token,
            user.PreferredCulture));
    }
}