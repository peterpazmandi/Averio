using Averio.Application.Identity;
using Averio.Domain.Households;

namespace Averio.Application.Accounts.Common;

public sealed class HouseholdMemberDirectory(IIdentityService identityService)
{
    public async Task<IReadOnlyDictionary<Guid, string?>> GetAsync(
        Household household,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string?>();
        // Sequential: Identity and repositories share one scoped EF context.
        foreach (var member in household.Members.OrderBy(member => member.Id))
        {
            var user = await identityService.FindByIdAsync(member.UserId, cancellationToken);
            names[member.Id] = user?.DisplayName;
        }
        return names;
    }
}
