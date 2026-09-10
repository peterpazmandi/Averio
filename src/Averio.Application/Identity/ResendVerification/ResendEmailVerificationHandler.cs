using FluentValidation;

namespace Averio.Application.Identity.ResendVerification;


public sealed class ResendEmailVerificationHandler(
    IValidator<ResendEmailVerificationCommand> validator,
    IIdentityService identityService,
    IEmailVerificationQueue emailVerificationQueue,
    IResendEmailVerificationThrottle requestThrottle)
{
    public async Task HandleAsync(ResendEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await identityService.FindByEmailAsync(command.Email.Trim(), cancellationToken);

        /*
         * Don't disclose whether the email exists.
         */
        if (user is null || user.EmailConfirmed)
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

        var token = await identityService
                            .GenerateEmailVerificationTokenAsync(user.Id, cancellationToken);

        if (token is null)
        {
            return;
        }

        /*
         * Queued, not awaited: delivery latency (SMTP/API round-trip)
         * must never leak into the response time for this endpoint.
         */
        emailVerificationQueue.Enqueue(new EmailVerificationJob(
            user.Id,
            user.Email,
            token,
            user.PreferredCulture));
    }
}