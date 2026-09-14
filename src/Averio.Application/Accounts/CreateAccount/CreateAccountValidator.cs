using Averio.Domain.Accounts;
using Averio.Domain.Common;
using FluentValidation;

namespace Averio.Application.Accounts.CreateAccount;

public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.HouseholdId).NotEmpty().WithErrorCode("Validation_HouseholdRequired");
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("Validation_AccountNameRequired")
            .Must(value => value.Trim().Length <= 100).WithErrorCode("Validation_AccountNameTooLong");
        RuleFor(x => x.Type).IsInEnum().WithErrorCode("Validation_AccountTypeInvalid");
        RuleFor(x => x.Scope).IsInEnum().WithErrorCode("Validation_AccountScopeInvalid");
        RuleFor(x => x.Visibility).IsInEnum().WithErrorCode("Validation_AccountVisibilityInvalid");
        RuleFor(x => x.Currency).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("Validation_CurrencyRequired")
            .Must(CurrencyCode.IsSupported).WithErrorCode("Validation_CurrencyInvalid");
        RuleFor(x => x.BalanceAnchorAmount).PrecisionScale(19, 4, true)
            .WithErrorCode("Validation_AccountBalanceInvalid");
        RuleFor(x => x.BalanceAnchorDate).NotEqual(default(DateOnly))
            .WithErrorCode("Validation_AccountBalanceDateRequired");

        RuleFor(x => x.OwnerHouseholdMemberIds).Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode("Validation_AccountOwnersRequired")
            .Must(ids => ids.All(id => id != Guid.Empty)
                && ids.Distinct().Count() == ids.Count)
            .WithErrorCode("Validation_AccountOwnersInvalid");
        RuleFor(x => x.OwnerHouseholdMemberIds)
            .Must(ids => ids is not null && ids.Count > 0)
            .When(x => x.Scope == AccountScope.Personal)
            .WithErrorCode("Validation_PersonalAccountRequiresOwner");
        RuleFor(x => x.Visibility).Equal(AccountVisibility.Household)
            .When(x => x.Scope == AccountScope.Household)
            .WithErrorCode("Validation_HouseholdScopeMustBeVisible");
        RuleFor(x => x.IncludeInHouseholdReports).Equal(true)
            .When(x => x.Scope == AccountScope.Household)
            .WithErrorCode("Validation_HouseholdScopeMustBeIncluded");
        RuleFor(x => x.Scope).Equal(AccountScope.Personal)
            .When(x => x.Visibility == AccountVisibility.Private)
            .WithErrorCode("Validation_PrivateAccountMustBePersonal");
        RuleFor(x => x.IncludeInHouseholdReports).Equal(false)
            .When(x => x.Visibility == AccountVisibility.Private)
            .WithErrorCode("Validation_PrivateAccountCannotBeIncluded");
    }
}
