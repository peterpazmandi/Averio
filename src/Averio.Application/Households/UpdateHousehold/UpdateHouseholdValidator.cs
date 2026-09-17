using Averio.Domain.Common;
using FluentValidation;

namespace Averio.Application.Households.UpdateHousehold
{
    public sealed class UpdateHouseholdValidator
        : AbstractValidator<UpdateHouseholdCommand>
    {
        public UpdateHouseholdValidator()
        {
            RuleFor(x => x.Name)
                .Must(value => value is null || value.Trim().Length >= 2)
                .WithErrorCode("Validation_HouseholdNameTooShort")
                .MaximumLength(120)
                .WithErrorCode("Validation_HouseholdNameTooLong")
                .When(x => x.Name is not null);

            RuleFor(x => x.CountryCode)
                .Matches("^[A-Za-z]{2}$")
                .WithErrorCode("Validation_CountryInvalid")
                .When(x => x.CountryCode is not null);

            RuleFor(x => x.PreferredCurrency)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithErrorCode("Validation_CurrencyRequired")
                .Must(value => CurrencyCode.IsSupported(value))
                .WithErrorCode("Validation_CurrencyInvalid")
                .When(
                    x => x.PreferredCurrency is not null,
                    ApplyConditionTo.AllValidators);
        }
    }
}