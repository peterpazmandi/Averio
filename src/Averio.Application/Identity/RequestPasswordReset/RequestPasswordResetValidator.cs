using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;

namespace Averio.Application.Identity.RequestPasswordReset
{
    public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
    {
        public RequestPasswordResetValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithErrorCode(
                    "Validation_EmailRequired")
                .EmailAddress()
                .WithErrorCode(
                    "Validation_EmailInvalid");
        }
    }
}