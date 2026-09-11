using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Identity;

namespace Averio.Application.Onboarding.GetOnboardingStatus;

public sealed class GetOnboardingStatusHandler(
    ICurrentUser currentUser,
    IIdentityService identityService,
    IHouseholdRepository householdRepository)
{
    public async Task<OnboardingStatusResult> HandleAsync(
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var user = await identityService.FindByIdAsync(
                    userId,
                    cancellationToken)
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByUserIdAsync(
                        userId,
                        cancellationToken)
            ?? throw new HouseholdRequiredException();

        return new OnboardingStatusResult(
            household.IsOnboardingCompleted,
            user.DisplayName,
            household.Id,
            household.Name,
            household.CountryCode,
            household.ReportingCurrency?.ToString());
    }
}