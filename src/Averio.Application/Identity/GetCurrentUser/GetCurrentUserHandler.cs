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
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var user = await identityService.FindByIdAsync(userId, cancellationToken)
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new HouseholdRequiredException();

        var membership = household.Members.Single(x => x.UserId == userId && x.LeftAtUtc == null);

        return new CurrentUserResult(
            user.Id,
            user.Email,
            user.DisplayName,
            user.EmailConfirmed,
            household.Id,
            membership.Role,
            !household.IsFinancialSetupCompleted);
    }
}