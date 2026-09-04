using Averio.Application.Common.Exceptions;
using FluentValidation;

namespace Averio.Application.Identity.VerifyEmail;

public sealed class VerifyEmailHandler(
    IValidator<VerifyEmailCommand> validator,
    IIdentityService identityService)
{
    public async Task HandleAsync(
        VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var status =
            await identityService.ConfirmEmailAsync(
                command.UserId,
                command.Token,
                cancellationToken);

        switch (status)
        {
            case EmailConfirmationStatus.Succeeded:
            case EmailConfirmationStatus.AlreadyConfirmed:
                return;

            default:
                throw new InvalidEmailVerificationTokenException();
        }
    }
}