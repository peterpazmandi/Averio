using Averio.Domain.Accounts;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal static class VisibleTransactionQuery
{
    public static IQueryable<Transaction> VisibleTransactions(
        this AverioDbContext dbContext,
        Guid householdId,
        Guid householdMemberId)
    {
        return dbContext.Transactions
            .Join(
                dbContext.Accounts.Include(account => account.Owners),
                transaction => new { transaction.HouseholdId, transaction.AccountId },
                account => new { account.HouseholdId, AccountId = account.Id },
                (transaction, account) => new
                {
                    Transaction = transaction,
                    Account = account
                })
            .Where(item =>
                item.Transaction.HouseholdId == householdId
                && (
                    item.Account.Visibility == AccountVisibility.Household
                    || item.Account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)
                )
                && (
                    item.Transaction.Visibility == TransactionVisibility.Household
                    || item.Account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)
                ))
            .Select(item => item.Transaction);
    }

}
