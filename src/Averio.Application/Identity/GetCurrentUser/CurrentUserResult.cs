using Averio.Domain.Households;

namespace Averio.Application.Identity.GetCurrentUser
{
    public sealed record CurrentUserResult(
        Guid UserId,
        string Email,
        string? DisplayName,
        bool EmailVerified,
        Guid HouseholdId,
        HouseholdRole HouseholdRole,
        bool OnboardingRequired);
}