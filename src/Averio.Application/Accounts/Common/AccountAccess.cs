using Averio.Domain.Accounts;
using Averio.Domain.Households;

namespace Averio.Application.Accounts.Common;

public static class AccountAccess
{
    public static bool CanModify(
        Account account,
        HouseholdMember membership)
    {
        if (account.HouseholdId != membership.HouseholdId || membership.LeftAtUtc is not null || account.CutoverAtUtc is not null)
        {
            return false;
        }

        return account.Scope == AccountScope.Household
            || account.Owners.Any(owner => owner.HouseholdMemberId == membership.Id);
    }
}

