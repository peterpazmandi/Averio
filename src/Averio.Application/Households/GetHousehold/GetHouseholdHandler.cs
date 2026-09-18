using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Households.GetHousehold;

public sealed class GetHouseholdHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository)
{
    public async Task<HouseholdResult> HandleAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByIdForUserAsync(
                    householdId,
                    userId,
                    cancellationToken)
            ?? throw new NotFoundException("household", householdId);

        var membership = household.Members.Single(x => x.UserId == userId);

        return new HouseholdResult(
            household.Id,
            household.Name,
            household.CountryCode,
            household.ReportingCurrency?.ToString(),
            household.IsOnboardingCompleted,
            membership.Role
                .ToString()
                .ToLowerInvariant());
    }
}