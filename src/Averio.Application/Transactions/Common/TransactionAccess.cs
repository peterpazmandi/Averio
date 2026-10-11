using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Transactions;

namespace Averio.Application.Transactions.Common;

public static class TransactionAccess
{
    public static bool CanModify(Account account, HouseholdMember membership)
    {
        if (account.HouseholdId != membership.HouseholdId || membership.LeftAtUtc is not null || account.CutoverAtUtc is not null)
        {
            return false;
        }

        return account.Scope == AccountScope.Household
            || account.Owners.Any(owner => owner.HouseholdMemberId == membership.Id);
    }

    public static bool IsVisibilityAllowed(Account account, TransactionVisibility visibility)
    {
        if (account.Visibility == AccountVisibility.Private)
        {
            return visibility == TransactionVisibility.Private;
        }

        if (account.Scope == AccountScope.Household)
        {
            return visibility == TransactionVisibility.Household;
        }

        return true;
    }

    public static bool ContributesToHouseholdCashFlow(Transaction transaction)
        => transaction.Status == TransactionStatus.Posted
            && transaction.Visibility == TransactionVisibility.Household;
}

