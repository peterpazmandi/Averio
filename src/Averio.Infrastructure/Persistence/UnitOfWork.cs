using Averio.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Averio.Infrastructure.Persistence;

internal sealed class UnitOfWork(AverioDbContext dbContext) : IUnitOfWork
{
    public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new ApplicationTransaction(transaction);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
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