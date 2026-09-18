using Averio.Application.Accounts.CreateAccount;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Households;
using FluentValidation;
using FluentValidation.Results;

namespace Averio.Application.Accounts.Common;

public sealed class AccountCreator(
    IValidator<CreateAccountCommand> validator,
    IAccountRepository accountRepository,
    TimeProvider timeProvider)
{
    public async Task<Account> CreateAsync(
        CreateAccountCommand command,
        Household household,
        HouseholdMember membership,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        if (household.Id != command.HouseholdId
            || membership.HouseholdId != household.Id
            || !household.Members.Any(member => member.Id == membership.Id
                && member.UserId == membership.UserId))
        {
            throw new NotFoundException("household", command.HouseholdId);
        }

        var members = household.Members.Select(member => member.Id).ToHashSet();
        if (command.OwnerHouseholdMemberIds.Any(id => !members.Contains(id)))
            throw OwnerValidation("Validation_AccountOwnerOutsideHousehold");

        // Personal creation must not assign private access only to somebody else.
        if (command.Scope == AccountScope.Personal
            && !command.OwnerHouseholdMemberIds.Contains(membership.Id))
            throw OwnerValidation("Validation_PersonalAccountMustIncludeYou");

        var account = Account.Create(
            household.Id,
            command.OwnerHouseholdMemberIds,
            command.Name,
            command.Type,
            command.Scope,
            command.Visibility,
            command.IncludeInHouseholdReports,
            CurrencyCode.From(command.Currency),
            command.BalanceAnchorAmount,
            command.BalanceAnchorDate,
            timeProvider.GetUtcNow());

        accountRepository.Add(account);
        return account;
    }

    private static ValidationException OwnerValidation(string code)
    {
        return new ValidationException(
        [
            new ValidationFailure("OwnerHouseholdMemberIds", code)
            {
                ErrorCode = code
            }
        ]);
    }
}
