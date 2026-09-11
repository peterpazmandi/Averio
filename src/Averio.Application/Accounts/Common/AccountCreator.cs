using Averio.Application.Accounts.CreateAccount;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Households;
using FluentValidation;

namespace Averio.Application.Accounts.Common;

public sealed class AccountCreator(
    IValidator<CreateAccountCommand> validator,
    IAccountRepository accountRepository,
    TimeProvider timeProvider)
{
    public async Task<Account> CreateAsync(
        CreateAccountCommand command,
        HouseholdMember membership,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        if (membership.HouseholdId != command.HouseholdId)
        {
            throw new NotFoundException("household", command.HouseholdId);
        }

        var currency = Enum.Parse<CurrencyCode>(
            command.Currency.Trim(),
            ignoreCase: true);

        var account = Account.Create(
            command.HouseholdId,
            membership.Id,
            command.Name,
            command.Type,
            command.Ownership,
            command.Visibility,
            command.IncludeInHouseholdReports,
            currency,
            command.BalanceAnchorAmount,
            command.BalanceAnchorDate,
            timeProvider.GetUtcNow());

        accountRepository.Add(account);

        return account;
    }
}