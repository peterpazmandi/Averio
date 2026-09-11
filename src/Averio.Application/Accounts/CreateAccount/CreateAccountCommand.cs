using Averio.Domain.Accounts;

namespace Averio.Application.Accounts.CreateAccount;

public sealed record CreateAccountCommand(
    Guid HouseholdId,
    string Name,
    AccountType Type,
    AccountOwnership Ownership,
    AccountVisibility Visibility,
    bool IncludeInHouseholdReports,
    string Currency,
    decimal BalanceAnchorAmount,
    DateOnly BalanceAnchorDate);