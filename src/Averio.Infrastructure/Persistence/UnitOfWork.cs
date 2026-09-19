using Averio.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Averio.Application.Common.Exceptions;

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
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("account_changed", "Error_AccountChanged");
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