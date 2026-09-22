using Averio.Application.Common.Abstractions;
using Averio.Domain.Transactions;

namespace Averio.Application.Transactions.Common;

public sealed class TransactionAggregateWriter(
    ITransactionAggregateRepository aggregateRepository)
{
    public async Task ApplyAsync(
        Transaction transaction,
        int direction,
        CancellationToken cancellationToken)
    {
        if (direction is not (1 or -1))
            throw new ArgumentOutOfRangeException(nameof(direction));

        if (!TransactionAccess.ContributesToHouseholdCashFlow(transaction))
            return;

        await aggregateRepository.ApplyDeltaAsync(
            transaction.HouseholdId,
            transaction.AccountId,
            TransactionAggregateDimension.Account,
            transaction.AccountId,
            transaction.Currency.Value,
            transaction.OccurredOn,
            transaction.Kind,
            direction * transaction.Amount,
            direction,
            cancellationToken);

        foreach (var group in transaction.Items.GroupBy(item => item.CategoryId))
        {
            await aggregateRepository.ApplyDeltaAsync(
                transaction.HouseholdId,
                transaction.AccountId,
                TransactionAggregateDimension.Category,
                group.Key,
                transaction.Currency.Value,
                transaction.OccurredOn,
                transaction.Kind,
                direction * group.Sum(item => item.Amount),
                direction * group.Count(),
                cancellationToken);
        }
    }
}
