using Averio.Application.Common.Abstractions;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class AccountBalanceReader(AverioDbContext dbContext)
    : IAccountBalanceReader
{
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetCurrentBalancesAsync(
        Guid householdId,
        Guid householdMemberId,
        IReadOnlyCollection<Guid> accountIds,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var ids = accountIds.Distinct().ToArray();

        var anchors = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.HouseholdId == householdId
                && ids.Contains(account.Id))
            .Select(account => new
            {
                account.Id,
                account.BalanceAnchorAmount,
            })
            .ToListAsync(cancellationToken);

        var balances = anchors.ToDictionary(
            account => account.Id,
            account => account.BalanceAnchorAmount);

        var movements = await (
            from transaction in dbContext.Transactions.AsNoTracking()
            join account in dbContext.Accounts.AsNoTracking()
                on new { transaction.HouseholdId, transaction.AccountId }
                equals new { account.HouseholdId, AccountId = account.Id }
            where transaction.HouseholdId == householdId
                && ids.Contains(transaction.AccountId)
                && transaction.Status == TransactionStatus.Posted
                && transaction.OccurredOn >= account.BalanceAnchorDate
                && transaction.OccurredOn <= asOf
                && (
                    transaction.Visibility == TransactionVisibility.Household
                    || account.Owners.Any(owner =>
                        owner.HouseholdMemberId == householdMemberId)
                )
            select new
            {
                transaction.AccountId,
                transaction.Kind,
                transaction.Amount,
            })
            .ToListAsync(cancellationToken);

        foreach (var movement in movements)
        {
            if (!balances.TryGetValue(movement.AccountId, out var balance))
                continue;

            balances[movement.AccountId] = movement.Kind == TransactionKind.Income
                ? balance + movement.Amount
                : balance - movement.Amount;
        }

        return balances;
    }
}
