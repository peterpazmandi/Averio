using Averio.Domain.Accounts;
using Averio.Domain.Households;

namespace Averio.Application.Accounts.Common;

public static class AccountAccess
{
    public static bool CanModify(
        Account account,
        HouseholdMember membership)
    {
        if (account.HouseholdId != membership.HouseholdId)
        {
            return false;
        }

        return account.Ownership == AccountOwnership.Household
            || account.OwnerHouseholdMemberId == membership.Id;
    }
}