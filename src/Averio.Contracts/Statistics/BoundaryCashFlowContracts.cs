namespace Averio.Contracts.Statistics;

public sealed record BoundaryMonthResponse(string Currency, DateOnly Month,
    string PayoutsToExcludedAccounts, string ReturnsToHousehold, int TransferCount);

public sealed record BoundaryCashFlowResponse(DateOnly From, DateOnly To,
    IReadOnlyList<BoundaryMonthResponse> Months);
