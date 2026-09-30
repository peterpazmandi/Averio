using Averio.Application.Common.Abstractions;
using Averio.Domain.Transactions;
using Averio.Domain.Accounts;
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
                && ids.Contains(account.Id)
                && (account.Visibility == AccountVisibility.Household
                    || account.Owners.Any(o => o.HouseholdMemberId == householdMemberId)))
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

        // Each visible leg contributes in its own account currency and anchor window.
        // Never inspect the other account to derive this account's balance.
        var visibleAccounts = dbContext.Accounts.AsNoTracking().Where(a =>
            a.HouseholdId == householdId && ids.Contains(a.Id)
            && (a.Visibility == AccountVisibility.Household
                || a.Owners.Any(o => o.HouseholdMemberId == householdMemberId)));
        var postedTransfers = dbContext.Transfers.AsNoTracking().Where(t =>
            t.HouseholdId == householdId && t.VoidedAt == null && t.OccurredOn <= asOf);
        var outgoing = (from t in postedTransfers
            join a in visibleAccounts on t.SourceAccountId equals a.Id
            where t.OccurredOn >= a.BalanceAnchorDate
            select new { AccountId = a.Id, Amount = -t.SentAmount });
        var incoming = (from t in postedTransfers
            join a in visibleAccounts on t.DestinationAccountId equals a.Id
            where t.OccurredOn >= a.BalanceAnchorDate
            select new { AccountId = a.Id, Amount = t.ReceivedAmount });
        // UNION ALL keeps both legs in one PostgreSQL statement snapshot.
        var legs = await outgoing.Concat(incoming).ToListAsync(cancellationToken);
        foreach (var leg in legs)
            if (balances.TryGetValue(leg.AccountId, out var balance))
                balances[leg.AccountId] = balance + leg.Amount;

        return balances;
    }
}
