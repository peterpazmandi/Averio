using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Averio.Infrastructure.Persistence;

internal sealed class UnitOfWork(AverioDbContext dbContext) : IUnitOfWork
{
    public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new ApplicationTransaction(transaction);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            if (exception.Entries.Any(entry => entry.Entity is Averio.Domain.Households.Household))
                throw new ConflictException("household_changed", "Error_HouseholdChanged");

            if (exception.Entries.Any(entry => entry.Entity is Averio.Domain.Activities.Activity or Averio.Domain.Activities.ActivityAssignment))
                throw new ConflictException("activity_changed", "Error_ActivityChanged");

            if (exception.Entries.Any(entry => entry.Entity is Averio.Domain.Categories.Category))
                throw new ConflictException("category_changed", "Error_CategoryChanged");

            if (exception.Entries.Any(entry => entry.Entity is Averio.Domain.Transfers.Transfer))
                throw new ConflictException("transfer_changed", "Error_TransferChanged");

            if (exception.Entries.Any(entry =>
                entry.Entity is Transaction
                    or TransactionItem
                    or TransactionLabel))
            {
                throw new ConflictException(
                    "transaction_changed",
                    "Error_TransactionChanged");
            }

            throw new ConflictException(
                "account_changed",
                "Error_AccountChanged");
        }
    }

    private sealed class ApplicationTransaction(IDbContextTransaction transaction) : IApplicationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
            => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken)
            => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync()
            => transaction.DisposeAsync();
    }
}

