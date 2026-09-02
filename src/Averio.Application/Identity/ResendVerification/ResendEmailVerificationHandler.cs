using FluentValidation;

namespace Averio.Application.Identity.ResendVerification;


public sealed class ResendEmailVerificationHandler(
    IValidator<ResendEmailVerificationCommand> validator,
    IIdentityService identityService,
    IEmailVerificationSender emailVerificationSender)
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

        var token = await identityService
                            .GenerateEmailVerificationTokenAsync(user.Id, cancellationToken);

        if (token is null)
        {
            return;
        }

        await emailVerificationSender.TrySendAsync(
            user.Id,
            user.Email,
            token,
            user.PreferredCulture,
            cancellationToken);
    }
}