using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Statistics.GetBoundaryCashFlow;

public sealed record BoundaryFlowMonth(string Currency, DateOnly Month,
    decimal PayoutsToExcludedAccounts, decimal ReturnsToHousehold, int TransferCount);

public interface IBoundaryFlowRepository
{
    Task<IReadOnlyList<BoundaryFlowMonth>> GetMonthsAsync(Guid householdId,
        DateOnly from, DateOnly to, CancellationToken ct);
}

public sealed class BoundaryCashFlowHandler(ICurrentUser currentUser,
    IHouseholdRepository households, IBoundaryFlowRepository repository)
{
    public async Task<IReadOnlyList<BoundaryFlowMonth>> GetAsync(Guid householdId,
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (from == default || to == default || from > to || to.DayNumber - from.DayNumber > 1096)
            throw new BusinessRuleException("boundary_flow_date_range_invalid", "Validation_CashFlowDateRangeInvalid");
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        // Aggregated calendar months, with no counterparty account/transfer identity.
        return await repository.GetMonthsAsync(householdId, from, to, ct);
    }
}
