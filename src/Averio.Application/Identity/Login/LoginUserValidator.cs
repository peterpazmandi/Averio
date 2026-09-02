using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FluentValidation;

namespace Averio.Application.Identity.Login
{
public sealed class LoginUserValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithErrorCode("Validation_EmailRequired")
            .EmailAddress()
                .WithErrorCode("Validation_EmailInvalid");

        RuleFor(x => x.Password)
            .NotEmpty()
                .WithErrorCode("Validation_PasswordRequired");
    }
}
}