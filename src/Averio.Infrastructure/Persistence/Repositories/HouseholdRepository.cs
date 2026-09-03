using Averio.Application.Common.Abstractions;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class HouseholdRepository(AverioDbContext dbContext) : IHouseholdRepository
{
    public void Add(Household household)
    {
        dbContext.Households.Add(household);
    }

    public Task<HouseholdMember?> GetMembershipByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.HouseholdMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);
    }
}