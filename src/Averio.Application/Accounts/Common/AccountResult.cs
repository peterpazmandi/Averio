using Averio.Domain.Accounts;

namespace Averio.Application.Accounts.Common;

public sealed record AccountOwnerResult(Guid HouseholdMemberId, string? DisplayName);

public sealed record AccountResult(
    Guid Id,
    Guid HouseholdId,
    IReadOnlyList<AccountOwnerResult> Owners,
    string Name,
    string Type,
    string Scope,
    string Visibility,
    bool IncludeInHouseholdReports,
    string Currency,
    decimal BalanceAnchorAmount,
    DateOnly BalanceAnchorDate,
    string Status,
    DateTimeOffset UpdatedAt)
{
    public static AccountResult From(
        Account account,
        IReadOnlyDictionary<Guid, string?> memberNames)
    {
        var owners = account.Owners
            .OrderBy(owner => owner.HouseholdMemberId)
            .Select(owner => new AccountOwnerResult(
                owner.HouseholdMemberId,
                memberNames.GetValueOrDefault(owner.HouseholdMemberId)))
            .ToArray();

        return new AccountResult(
            account.Id, account.HouseholdId, owners, account.Name,
            account.Type.ToString(), account.Scope.ToString(),
            account.Visibility.ToString(), account.IncludeInHouseholdReports,
            account.Currency.Value, account.BalanceAnchorAmount,
            account.BalanceAnchorDate, account.Status.ToString(), account.UpdatedAt);
    }
}
