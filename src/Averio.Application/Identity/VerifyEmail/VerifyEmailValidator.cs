using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;

namespace Averio.Application.Identity.VerifyEmail
{
public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
                .WithErrorCode("Validation_UserIdRequired");

        RuleFor(x => x.Token)
            .NotEmpty()
                .WithErrorCode("Validation_TokenRequired");
    }
}
}