using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Accounts.Common;

namespace Averio.Application.Accounts.GetAccounts;

public sealed class GetAccountsHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    HouseholdMemberDirectory memberDirectory)
{
    public async Task<IEnumerable<AccountResult>> HandleAsync(
        Guid householdId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household =
            await householdRepository.GetByIdForUserAsync(
                householdId,
                userId,
                cancellationToken)
            ?? throw new NotFoundException("household", householdId);

        var membership = household.Members.Single(
            member => member.UserId == userId);

        var accounts = await accountRepository.GetVisibleAsync(
            householdId,
            membership.Id,
            includeArchived,
            cancellationToken);

        var names = await memberDirectory.GetAsync(household, cancellationToken);
        return accounts
            .Select(account => AccountResult.From(account, names))
            .ToArray();
    }
}
