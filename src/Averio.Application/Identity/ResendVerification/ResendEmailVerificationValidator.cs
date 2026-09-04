using FluentValidation;

namespace Averio.Application.Identity.ResendVerification
{
    public sealed class ResendEmailVerificationValidator : AbstractValidator<ResendEmailVerificationCommand>
    {
        public ResendEmailVerificationValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                    .WithErrorCode("Validation_EmailRequired")
                .EmailAddress()
                    .WithErrorCode("Validation_EmailInvalid")
                .MaximumLength(320)
                    .WithErrorCode("Validation_EmailTooLong");
        }
    }
}