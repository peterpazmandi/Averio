using Averio.Application.Common.Abstractions;
using Averio.Domain.Households;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class HouseholdRepository(AverioDbContext dbContext) : IHouseholdRepository
{
    public void Add(Household household)
    {
        dbContext.Households.Add(household);
    }

    public Task<HouseholdMember?> GetMembershipByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.HouseholdMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);
    }

    public Task<Household?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Households
            .Include(x => x.Members)
            .SingleOrDefaultAsync(
                household => household.Members.Any(member => member.UserId == userId),
                cancellationToken);
    }

    public Task<Household?> GetByIdForUserAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.Households
            .Include(x => x.Members)
            .SingleOrDefaultAsync(
                household =>
                    household.Id == householdId
                    && household.Members.Any(
                        member =>
                            member.UserId == userId),
                cancellationToken);
    }
}