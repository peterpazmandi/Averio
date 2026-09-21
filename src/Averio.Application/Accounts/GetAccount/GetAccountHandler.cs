using Averio.Application.Accounts.Common;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Accounts.GetAccount;

public sealed class GetAccountHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    IAccountBalanceReader accountBalanceReader,
    HouseholdMemberDirectory memberDirectory,
    TimeProvider timeProvider)
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

        var asOf = DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);

        var balances = await accountBalanceReader.GetCurrentBalancesAsync(
            householdId,
            membership.Id,
            [account.Id],
            asOf,
            cancellationToken);

        var names = await memberDirectory.GetAsync(
            household,
            cancellationToken);

        return AccountResult.From(
            account,
            names,
            balances.GetValueOrDefault(
                account.Id,
                account.BalanceAnchorAmount));
    }
}
