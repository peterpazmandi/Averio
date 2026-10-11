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
                x => x.UserId == userId && x.LeftAtUtc == null,
                cancellationToken);
    }

    public async Task<Household?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var id = await dbContext.HouseholdMembers.AsNoTracking().Where(x => x.UserId == userId && x.LeftAtUtc == null)
            .Select(x => (Guid?)x.HouseholdId).SingleOrDefaultAsync(cancellationToken);
        return id is null ? null : await GetByIdForUserAsync(id.Value, userId, cancellationToken);
    }

    public Task<Household?> GetByIdForUserAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
            return GetByIdForUserForUpdateAsync(householdId, userId, cancellationToken);
        return dbContext.Households
            .Include(x => x.Members)
            .SingleOrDefaultAsync(
                household =>
                    household.Id == householdId
                    && household.ArchivedAtUtc == null
                    && household.Members.Any(
                        member =>
                            member.UserId == userId && member.LeftAtUtc == null),
                cancellationToken);
    }

    public async Task<Household?> GetByIdForUserForUpdateAsync(
    Guid householdId,
    Guid userId,
    CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A transaction is required when locking a household.");
        }

        var households = await dbContext.Households
            .FromSqlInterpolated(
                $"""
                    SELECT h.*
                    FROM households AS h
                    WHERE h.id = {householdId}
                    AND EXISTS
                    (
                        SELECT 1
                        FROM household_members AS m
                        WHERE m.household_id = h.id
                            AND m.user_id = {userId}
                            AND m.left_at_utc IS NULL
                            AND h.archived_at_utc IS NULL
                    )
                    FOR UPDATE OF h
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var household = households.SingleOrDefault();

        if (household is null)
        {
            return null;
        }

        // A departure may commit while this query waits for the parent lock.
        // Recheck membership with a fresh READ COMMITTED statement after acquiring it.
        await dbContext.Entry(household).ReloadAsync(cancellationToken);
        if (household.ArchivedAtUtc is not null || !await dbContext.HouseholdMembers
                .AsNoTracking().AnyAsync(member => member.HouseholdId == householdId
                    && member.UserId == userId && member.LeftAtUtc == null, cancellationToken))
            return null;

        await dbContext.Entry(household)
            .Collection(value => value.Members)
            .LoadAsync(cancellationToken);

        return household;
    }
}
