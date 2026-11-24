namespace Averio.Domain.Transactions;

/// <summary>
/// A bank credit is recorded once. Attribution may be split, but cannot create or
/// consume more cash than the posted credit contains.
/// </summary>
public static class ReceivedCreditAllocationRule
{
    public static decimal Remaining(decimal creditAmount, IEnumerable<decimal> activeAllocations)
    {
        if (creditAmount <= 0 || decimal.Round(creditAmount, 4) != creditAmount)
            throw new ArgumentOutOfRangeException(nameof(creditAmount));

        decimal allocated = 0;
        foreach (var amount in activeAllocations)
        {
            if (amount <= 0 || decimal.Round(amount, 4) != amount)
                throw new ArgumentOutOfRangeException(nameof(activeAllocations));
            allocated = checked(allocated + amount);
            if (allocated > creditAmount)
                throw new ArgumentException("Allocations exceed the received bank credit.", nameof(activeAllocations));
        }

        return creditAmount - allocated;
    }

    public static void ValidateNew(decimal creditAmount, IEnumerable<decimal> activeAllocations,
        decimal requestedAmount, decimal targetRemaining)
    {
        if (requestedAmount <= 0 || decimal.Round(requestedAmount, 4) != requestedAmount
            || targetRemaining <= 0 || requestedAmount > targetRemaining
            || requestedAmount > Remaining(creditAmount, activeAllocations))
            throw new ArgumentException("The allocation exceeds available credit or target coverage.", nameof(requestedAmount));
    }
}

