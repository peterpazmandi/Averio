using Averio.Application.Accounts.Common;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Identity;
using Averio.Domain.Accounts;

namespace Averio.Application.Dashboard.GetDashboard;

public sealed class GetDashboardHandler(
    ICurrentUser currentUser,
    IIdentityService identityService,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    HouseholdMemberDirectory memberDirectory)
{
    public async Task<DashboardResult> HandleAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household =
            await householdRepository.GetByIdForUserAsync(
                householdId,
                userId,
                cancellationToken)
            ?? throw new NotFoundException(
                "household",
                householdId);

        if (!household.IsFinancialSetupCompleted)
        {
            throw new OnboardingRequiredException();
        }

        var user = await identityService.FindByIdAsync(
            userId,
            cancellationToken)
            ?? throw new AuthenticationFailedException();

        var membership = household.Members.Single(
            member => member.UserId == userId);

        var reportingCurrency =
            household.ReportingCurrency?.Value
            ?? throw new OnboardingRequiredException();

        var visibleAccounts =
            await accountRepository.GetVisibleAsync(
                householdId,
                membership.Id,
                false,
                cancellationToken);

        var activeAccounts = visibleAccounts
            .Where(account =>
                account.Status == AccountStatus.Active)
            .ToArray();

        var householdBalances = activeAccounts
            .Where(account =>
                account.Visibility == AccountVisibility.Household
                && account.IncludeInHouseholdReports)
            .GroupBy(account => account.Currency.Value)
            .Select(group => new HouseholdBalanceResult(
                group.Key,
                group.Sum(account => account.BalanceAnchorAmount),
                group.Min(account => account.BalanceAnchorDate),
                group.Max(account => account.BalanceAnchorDate)))
            .OrderByDescending(balance =>
                balance.Currency == reportingCurrency)
            .ThenBy(balance => balance.Currency)
            .ToArray();

        var names = await memberDirectory.GetAsync(household, cancellationToken);
        var accounts = activeAccounts
            .Select(account => AccountResult.From(account, names))
            .ToArray();

        return new DashboardResult(
            user.DisplayName,
            reportingCurrency,
            householdBalances,
            accounts);
    }
}
