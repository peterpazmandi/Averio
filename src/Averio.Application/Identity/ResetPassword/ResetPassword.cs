using Averio.Application.Common.Exceptions;
using FluentValidation;

namespace Averio.Application.Identity.ResetPassword;

public sealed class ResetPasswordHandler(IValidator<ResetPasswordCommand> validator, IIdentityService identityService)
{
    public async Task HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var result = await identityService.ResetPasswordAsync(
                command.UserId,
                command.Token,
                command.Password,
                cancellationToken);

        switch (result)
        {
            case PasswordResetStatus.Succeeded: return;
            case PasswordResetStatus.Rejected: throw new BusinessRuleException("password_rejected", ErrorResourceKeys.PasswordRejected);
            default:throw new InvalidPasswordResetTokenException();
        }
    }
}