using Averio.Application.Accounts.Common;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Identity;
using Averio.Application.Transactions.GetTransactions;
using Averio.Domain.Accounts;
using Averio.Domain.Transactions;

namespace Averio.Application.Dashboard.GetDashboard;

public sealed class GetDashboardHandler(
    ICurrentUser currentUser,
    IIdentityService identityService,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    IAccountBalanceReader accountBalanceReader,
    ITransactionRepository transactionRepository,
    ITransactionAggregateRepository aggregateRepository,
    HouseholdMemberDirectory memberDirectory,
    TimeProvider timeProvider)
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
            throw new OnboardingRequiredException();

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
            .Where(account => account.Status == AccountStatus.Active)
            .ToArray();

        var today = DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);

        var currentBalances = await accountBalanceReader.GetCurrentBalancesAsync(
            household.Id,
            membership.Id,
            activeAccounts.Select(account => account.Id).ToArray(),
            today,
            cancellationToken);

        var householdBalances = activeAccounts
            .Where(account =>
                account.Visibility == AccountVisibility.Household
                && account.IncludeInHouseholdReports)
            .GroupBy(account => account.Currency.Value)
            .Select(group => new HouseholdBalanceResult(
                group.Key,
                group.Sum(account =>
                    currentBalances.GetValueOrDefault(
                        account.Id,
                        account.BalanceAnchorAmount)),
                group.Min(account => account.BalanceAnchorDate),
                group.Max(account => account.BalanceAnchorDate)))
            .OrderByDescending(balance =>
                balance.Currency == reportingCurrency)
            .ThenBy(balance => balance.Currency)
            .ToArray();

        var names = await memberDirectory.GetAsync(
            household,
            cancellationToken);

        var accounts = activeAccounts
            .Select(account => AccountResult.From(
                account,
                names,
                currentBalances.GetValueOrDefault(
                    account.Id,
                    account.BalanceAnchorAmount)))
            .ToArray();

        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var sixMonthStart = currentMonth.AddMonths(-5);

        var cashFlowRows = await aggregateRepository.GetCashFlowAsync(
            household.Id,
            sixMonthStart,
            today,
            TransactionAggregatePeriod.Month,
            cancellationToken);

        var cashFlow = cashFlowRows
            .Select(row => new DashboardCashFlowPeriodResult(
                row.Currency,
                row.PeriodStart,
                row.IncomeAmount,
                row.ExpenseAmount,
                row.TransactionCount))
            .ToArray();

        var latestRows = await transactionRepository.GetLatestVisibleAsync(
            household.Id,
            membership.Id,
            6,
            cancellationToken);

        var latestTransactions = latestRows
            .Select(TransactionResult.From)
            .ToArray();

        return new DashboardResult(
            user.DisplayName,
            reportingCurrency,
            householdBalances,
            accounts,
            cashFlow,
            latestTransactions);
    }
}
