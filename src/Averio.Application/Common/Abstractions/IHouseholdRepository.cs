using Averio.Domain.Households;

namespace Averio.Application.Common.Abstractions;

public interface IHouseholdRepository
{
    void Add(Household household);

    Task<HouseholdMember?> GetMembershipByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Household?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Household?> GetByIdForUserAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken);
}