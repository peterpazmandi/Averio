using Averio.Application.Common.Exceptions;
using FluentValidation;

namespace Averio.Application.Identity.Login;

public sealed class LoginUserHandler(
    IValidator<LoginUserCommand> validator,
    IUserSessionService userSessionService)
{
    public async Task<bool> HandleAsync(LoginUserCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command,cancellationToken);

        var result = await userSessionService.SignInAsync(
                command.Email.Trim(),
                command.Password,
                command.RememberMe,
                cancellationToken);

        switch (result)
        {
            case SignInStatus.Succeeded:
                return false;

            case SignInStatus.RequiresTwoFactor:
                return true;

            case SignInStatus.EmailNotConfirmed:
                throw new EmailNotVerifiedException();

            case SignInStatus.LockedOut:
                throw new AccountLockedException();

            default:
                throw new AuthenticationFailedException();
        }
    }
}
