using Averio.Application.Accounts.Common;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Accounts.GetAccountOwnerOptions;



public sealed class GetAccountOwnerOptionsHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    HouseholdMemberDirectory memberDirectory)
{
    public async Task<AccountOwnerOptionsResult> HandleAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        var household = await householdRepository.GetByIdForUserAsync(
            householdId, userId, cancellationToken)
            ?? throw new NotFoundException("household", householdId);
        var membership = household.Members.Single(member => member.UserId == userId);
        var names = await memberDirectory.GetAsync(household, cancellationToken);

        return new AccountOwnerOptionsResult(
            membership.Id,
            names.Select(pair => new AccountOwnerResult(pair.Key, pair.Value)).ToArray());
    }
}
