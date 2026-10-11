using Averio.Domain.Accounts;

namespace Averio.Application.Accounts.Common;

public sealed record AccountOwnerResult(Guid HouseholdMemberId, string? DisplayName, bool IsFormerMember = false);

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
    decimal CurrentBalanceAmount,
    string Status,
    DateTimeOffset UpdatedAt, DateTimeOffset? CutoverAtUtc = null)
{
    public static AccountResult From(
        Account account,
        IReadOnlyDictionary<Guid, string?> memberNames,
        decimal currentBalanceAmount, IReadOnlyCollection<Guid>? activeMemberIds = null)
    {
        var owners = account.Owners
            .OrderBy(owner => owner.HouseholdMemberId)
            .Select(owner => new AccountOwnerResult(
                owner.HouseholdMemberId,
                memberNames.GetValueOrDefault(owner.HouseholdMemberId), activeMemberIds is not null && !activeMemberIds.Contains(owner.HouseholdMemberId)))
            .ToArray();

        return new AccountResult(
            account.Id,
            account.HouseholdId,
            owners,
            account.Name,
            account.Type.ToString(),
            account.Scope.ToString(),
            account.Visibility.ToString(),
            account.IncludeInHouseholdReports,
            account.Currency.Value,
            account.BalanceAnchorAmount,
            account.BalanceAnchorDate,
            currentBalanceAmount,
            account.Status.ToString(),
            account.UpdatedAt, account.CutoverAtUtc);
    }
}

