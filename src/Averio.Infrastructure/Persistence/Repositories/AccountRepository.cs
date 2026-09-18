using Averio.Application.Common.Abstractions;
using Averio.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(
    AverioDbContext dbContext)
    : IAccountRepository
{
    public void Add(Account account)
    {
        dbContext.Accounts.Add(account);
    }

    public Task<Account?> GetVisibleByIdAsync(
        Guid householdId,
        Guid memberId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        return VisibleAccounts(householdId, memberId)
            .SingleOrDefaultAsync(
                account => account.Id == accountId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> GetVisibleAsync(
        Guid householdId,
        Guid memberId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = VisibleAccounts(householdId, memberId)
            .AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(
                account => account.Status == AccountStatus.Active);
        }

        return await query
            .OrderBy(account => account.Name)
            .ThenBy(account => account.Id)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Account> VisibleAccounts(
        Guid householdId,
        Guid memberId)
    {
        return dbContext.Accounts.Include(account => account.Owners).Where(account =>
            account.HouseholdId == householdId
            && (
                account.Visibility == AccountVisibility.Household
                || account.Owners.Any(owner => owner.HouseholdMemberId == memberId)
            ));
    }
}
