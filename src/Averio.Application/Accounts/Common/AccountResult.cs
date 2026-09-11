using Averio.Domain.Accounts;

namespace Averio.Application.Accounts.Common;

public sealed record AccountResult(
    Guid Id,
    Guid HouseholdId,
    Guid? OwnerHouseholdMemberId,
    string Name,
    string Type,
    string Ownership,
    string Visibility,
    bool IncludeInHouseholdReports,
    string Currency,
    decimal BalanceAnchorAmount,
    DateOnly BalanceAnchorDate,
    string Status)
{
    public static AccountResult From(Account account)
    {
        return new AccountResult(
            account.Id,
            account.HouseholdId,
            account.OwnerHouseholdMemberId,
            account.Name,
            account.Type.ToString(),
            account.Ownership.ToString(),
            account.Visibility.ToString(),
            account.IncludeInHouseholdReports,
            account.Currency.ToString(),
            account.BalanceAnchorAmount,
            account.BalanceAnchorDate,
            account.Status.ToString());
    }
}