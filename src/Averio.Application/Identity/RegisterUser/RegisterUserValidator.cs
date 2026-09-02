using FluentValidation;

namespace Averio.Application.Identity.RegisterUser
{
public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithErrorCode("Validation_EmailRequired")
            .EmailAddress()
                .WithErrorCode("Validation_EmailInvalid")
            .MaximumLength(320)
                .WithErrorCode("Validation_EmailTooLong");

        RuleFor(x => x.Password)
            .NotEmpty()
                .WithErrorCode("Validation_PasswordRequired")
            .MinimumLength(12)
                .WithErrorCode("Validation_PasswordTooShort")
            .MaximumLength(128)
                .WithErrorCode("Validation_PasswordTooLong");
    }
}
}