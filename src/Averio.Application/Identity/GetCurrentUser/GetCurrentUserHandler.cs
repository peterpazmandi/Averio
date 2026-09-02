using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Identity.GetCurrentUser;

public sealed class GetCurrentUserHandler(
    ICurrentUser currentUser,
    IIdentityService identityService,
    IHouseholdRepository householdRepository)
{
    public async Task<CurrentUserResult> HandleAsync(
        CancellationToken cancellationToken)
    {
        var userId =
            currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var user =
            await identityService.FindByIdAsync(
                userId,
                cancellationToken)
            ?? throw new AuthenticationFailedException();

        var membership =
            await householdRepository
                .GetMembershipByUserIdAsync(
                    userId,
                    cancellationToken)
            ?? throw new HouseholdRequiredException();

        return new CurrentUserResult(
            user.Id,
            user.Email,
            user.EmailConfirmed,
            membership.HouseholdId,
            membership.Role);
    }
}