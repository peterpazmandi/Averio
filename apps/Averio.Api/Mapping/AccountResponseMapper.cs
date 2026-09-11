using Averio.Application.Accounts.Common;
using Averio.Contracts.Accounts;

namespace Averio.Api.Mapping;

internal static class AccountResponseMapper
{
    public static AccountResponse From(
        AccountResult result)
    {
        return new AccountResponse(
            result.Id,
            result.HouseholdId,
            result.OwnerHouseholdMemberId,
            result.Name,
            result.Type,
            result.Ownership,
            result.Visibility,
            result.IncludeInHouseholdReports,
            result.Currency,
            result.BalanceAnchorAmount,
            result.BalanceAnchorDate,
            result.Status);
    }

    public static IReadOnlyList<AccountResponse> From(
        IEnumerable<AccountResult> results)
    {
        return results
            .Select(From)
            .ToArray();
    }
}