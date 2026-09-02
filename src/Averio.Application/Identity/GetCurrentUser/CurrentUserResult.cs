using Averio.Domain.Households;

namespace Averio.Application.Identity.GetCurrentUser
{
    public sealed record CurrentUserResult(
        Guid UserId,
        string Email,
        bool EmailVerified,
        Guid HouseholdId,
        HouseholdRole HouseholdRole);
}