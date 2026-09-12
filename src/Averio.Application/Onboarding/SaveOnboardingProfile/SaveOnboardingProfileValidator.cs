using FluentValidation;

namespace Averio.Application.Onboarding.SaveOnboardingProfile;

public sealed class SaveOnboardingProfileValidator : AbstractValidator<SaveOnboardingProfileCommand>
{
    public SaveOnboardingProfileValidator()
    {
        RuleFor(x => x.DisplayName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("Validation_DisplayNameRequired")
            .Must(value => value.Trim().Length >= 2)
            .WithErrorCode("Validation_DisplayNameTooShort")
            .MaximumLength(80)
            .WithErrorCode("Validation_DisplayNameTooLong");
    }
}