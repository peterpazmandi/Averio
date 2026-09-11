using Averio.Application.Accounts.Common;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Accounts.GetAccount;

public sealed class GetAccountHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository)
{
    public async Task<AccountResult> HandleAsync(
        Guid householdId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household =
            await householdRepository.GetByIdForUserAsync(
                householdId,
                userId,
                cancellationToken)
            ?? throw new NotFoundException(
                "household",
                householdId);

        var membership = household.Members.Single(
            member => member.UserId == userId);

        var account =
            await accountRepository.GetVisibleByIdAsync(
                householdId,
                membership.Id,
                accountId,
                cancellationToken)
            ?? throw new NotFoundException(
                "account",
                accountId);

        return AccountResult.From(account);
    }
}