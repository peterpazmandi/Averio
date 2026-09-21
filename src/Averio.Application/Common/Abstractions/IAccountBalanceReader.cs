namespace Averio.Application.Common.Abstractions;

public interface IAccountBalanceReader
{
    Task<IReadOnlyDictionary<Guid, decimal>> GetCurrentBalancesAsync(
        Guid householdId,
        Guid householdMemberId,
        IReadOnlyCollection<Guid> accountIds,
        DateOnly asOf,
        CancellationToken cancellationToken);
}
