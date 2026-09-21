using Averio.Domain.Accounts;

namespace Averio.Application.Accounts.UpdateAccount;

public sealed record UpdateAccountCommand(
    Guid HouseholdId,
    Guid AccountId,
    string Name,
    AccountType Type,
    IReadOnlyList<Guid> OwnerHouseholdMemberIds,
    AccountVisibility Visibility,
    bool IncludeInHouseholdReports,
    DateTimeOffset ExpectedUpdatedAt);
