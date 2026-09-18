using Averio.Domain.Common;
using FluentValidation;

namespace Averio.Application.Onboarding.SaveOnboardingHousehold;

public sealed class SaveOnboardingHouseholdValidator
    : AbstractValidator<SaveOnboardingHouseholdCommand>
{
    public SaveOnboardingHouseholdValidator()
    {
        RuleFor(x => x.HouseholdName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("Validation_HouseholdNameRequired")
            .Must(value => value.Trim().Length >= 2)
            .WithErrorCode("Validation_HouseholdNameTooShort")
            .MaximumLength(120)
            .WithErrorCode("Validation_HouseholdNameTooLong");

        RuleFor(x => x.CountryCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("Validation_CountryRequired")
            .Matches("^[A-Za-z]{2}$")
            .WithErrorCode("Validation_CountryInvalid");

        RuleFor(x => x.PreferredCurrency)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("Validation_CurrencyRequired")
            .Must(value => CurrencyCode.IsSupported(value))
            .WithErrorCode("Validation_CurrencyInvalid");
    }
}