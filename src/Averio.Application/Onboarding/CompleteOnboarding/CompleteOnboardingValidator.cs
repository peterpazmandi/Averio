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
            .MinimumLength(2)
                .WithErrorCode(
                    "Validation_DisplayNameTooShort")
            .MaximumLength(80)
                .WithErrorCode(
                    "Validation_DisplayNameTooLong");

        RuleFor(x => x.HouseholdName)
            .NotEmpty()
                .WithErrorCode(
                    "Validation_HouseholdNameRequired")
            .MinimumLength(2)
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
                Enum.TryParse<CurrencyCode>(
                    value,
                    ignoreCase: true,
                    out _))
                .WithErrorCode(
                    "Validation_CurrencyInvalid");
    }
}
}