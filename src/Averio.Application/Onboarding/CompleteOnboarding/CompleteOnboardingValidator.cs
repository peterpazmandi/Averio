using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Averio.Domain.Common;
using FluentValidation;

namespace Averio.Infrastructure.Onboarding.CompleteOnboarding
{
    public sealed class CompleteOnboardingValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
                .WithErrorCode(
                    "Validation_DisplayNameRequired")
            .Must(value => value is null || value.Trim().Length >= 2)
                .WithErrorCode(
                    "Validation_DisplayNameTooShort")
            .MaximumLength(80)
                .WithErrorCode(
                    "Validation_DisplayNameTooLong");

        RuleFor(x => x.HouseholdName)
            .NotEmpty()
                .WithErrorCode(
                    "Validation_HouseholdNameRequired")
            .Must(value => value is null || value.Trim().Length >= 2)
                .WithErrorCode(
                    "Validation_HouseholdNameTooShort")
            .MaximumLength(120)
                .WithErrorCode(
                    "Validation_HouseholdNameTooLong");

        RuleFor(x => x.CountryCode)
            .NotEmpty()
                .WithErrorCode(
                    "Validation_CountryRequired")
            .Matches("^[A-Za-z]{2}$")
                .WithErrorCode(
                    "Validation_CountryInvalid");

        RuleFor(x => x.PreferredCurrency)
            .NotEmpty()
                .WithErrorCode(
                    "Validation_CurrencyRequired")
            .Must(value =>
                value is not null
                && Enum.GetNames<CurrencyCode>()
                    .Contains(value, StringComparer.OrdinalIgnoreCase))
                .WithErrorCode(
                    "Validation_CurrencyInvalid");
    }
}
}