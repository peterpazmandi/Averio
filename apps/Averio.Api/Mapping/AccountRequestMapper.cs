using Averio.Application.Accounts.CreateAccount;
using Averio.Contracts.Accounts;
using Averio.Domain.Accounts;

namespace Averio.Api.Mapping;

internal static class AccountRequestMapper
{
    public static CreateAccountCommand ToCommand(
        Guid householdId,
        CreateAccountRequest request)
    {
        return new CreateAccountCommand(
            householdId,
            request.Name,
            EnumParser.Parse<AccountType>(
                request.Type,
                nameof(request.Type),
                "Validation_AccountTypeInvalid"),
            EnumParser.Parse<AccountOwnership>(
                request.Ownership,
                nameof(request.Ownership),
                "Validation_AccountOwnershipInvalid"),
            EnumParser.Parse<AccountVisibility>(
                request.Visibility,
                nameof(request.Visibility),
                "Validation_AccountVisibilityInvalid"),
            request.IncludeInHouseholdReports,
            request.Currency,
            request.BalanceAnchorAmount,
            request.BalanceAnchorDate);
    }
}