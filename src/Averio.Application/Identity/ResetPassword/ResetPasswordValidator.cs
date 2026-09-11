using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;

namespace Averio.Application.Identity.ResetPassword
{
    public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
    {
        public ResetPasswordValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithErrorCode(
                    "Validation_UserIdRequired");

            RuleFor(x => x.Token)
                .NotEmpty()
                .WithErrorCode(
                    "Validation_TokenRequired");

            RuleFor(x => x.Password)
                .NotEmpty()
                .WithErrorCode(
                    "Validation_PasswordRequired")
                .MinimumLength(12)
                .WithErrorCode(
                    "Validation_PasswordTooShort")
                .MaximumLength(128)
                .WithErrorCode(
                    "Validation_PasswordTooLong");
        }
    }
}