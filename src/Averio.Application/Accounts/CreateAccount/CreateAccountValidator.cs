using Averio.Domain.Accounts;
using Averio.Domain.Common;
using FluentValidation;

namespace Averio.Application.Accounts.CreateAccount;

public sealed class CreateAccountValidator
    : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.HouseholdId)
            .NotEmpty()
            .WithErrorCode("Validation_HouseholdRequired");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("Validation_AccountNameRequired")
            .Must(value => value.Trim().Length <= 100)
            .WithErrorCode("Validation_AccountNameTooLong");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithErrorCode("Validation_AccountTypeInvalid");

        RuleFor(x => x.Ownership)
            .IsInEnum()
            .WithErrorCode("Validation_AccountOwnershipInvalid");

        RuleFor(x => x.Visibility)
            .IsInEnum()
            .WithErrorCode("Validation_AccountVisibilityInvalid");

        RuleFor(x => x.Currency)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("Validation_CurrencyRequired")
            .Must(value => CurrencyCode.IsSupported(value))
            .WithErrorCode("Validation_CurrencyInvalid");

        RuleFor(x => x.BalanceAnchorAmount)
            .PrecisionScale(19, 4, true)
            .WithErrorCode("Validation_AccountBalanceInvalid");

        RuleFor(x => x.BalanceAnchorDate)
            .NotEqual(default(DateOnly))
            .WithErrorCode("Validation_AccountBalanceDateRequired");

        RuleFor(x => x.Visibility)
            .Equal(AccountVisibility.Household)
            .When(x => x.Ownership == AccountOwnership.Household)
            .WithErrorCode("Validation_SharedAccountMustBeVisible");

        RuleFor(x => x.IncludeInHouseholdReports)
            .Equal(true)
            .When(x => x.Ownership == AccountOwnership.Household)
            .WithErrorCode("Validation_SharedAccountMustBeIncluded");

        RuleFor(x => x.Ownership)
            .Equal(AccountOwnership.Personal)
            .When(x => x.Visibility == AccountVisibility.Private)
            .WithErrorCode("Validation_PrivateAccountMustBePersonal");

        RuleFor(x => x.IncludeInHouseholdReports)
            .Equal(false)
            .When(x => x.Visibility == AccountVisibility.Private)
            .WithErrorCode("Validation_PrivateAccountCannotBeIncluded");
    }
}