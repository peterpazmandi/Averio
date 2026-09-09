using Averio.Domain.Common;
using FluentValidation;

namespace Averio.Application.Households.UpdateHousehold
{
    public sealed class UpdateHouseholdValidator : AbstractValidator<UpdateHouseholdCommand>
    {
        public UpdateHouseholdValidator()
        {
            RuleFor(x => x.Name)
                .Must(value => value is null || value.Trim().Length >= 2)
                .MaximumLength(120)
                .When(x => x.Name is not null);

            RuleFor(x => x.CountryCode)
                .Matches("^[A-Za-z]{2}$")
                .When(x => x.CountryCode is not null);

            RuleFor(x => x.PreferredCurrency)
                .Must(value =>
                    value is null
                    || Enum.TryParse<CurrencyCode>(
                        value,
                        true,
                        out _));
        }
    }
}