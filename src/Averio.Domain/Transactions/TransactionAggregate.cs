using Averio.Domain.Common;

namespace Averio.Domain.Transactions;

public sealed class TransactionAggregate
{
    private TransactionAggregate() { }

    public Guid HouseholdId { get; private set; }
    public Guid AccountId { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public TransactionAggregatePeriod Period { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public decimal IncomeAmount { get; private set; }
    public decimal ExpenseAmount { get; private set; }
    public int TransactionCount { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
