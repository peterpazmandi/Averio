using Averio.Domain.Households;

namespace Averio.Application.Common.Abstractions;

public interface IUnitOfWork
{
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}

public interface IHouseholdRepository
{
    void Add(Household household);

    Task<HouseholdMember?> GetMembershipByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}