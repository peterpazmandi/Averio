using Averio.Domain.Accounts;

namespace Averio.Application.Common.Abstractions;

public interface IAccountRepository
{
    void Add(Account account);

    Task<Account?> GetVisibleByIdAsync(
        Guid householdId,
        Guid memberId,
        Guid accountId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Account>> GetVisibleAsync(
        Guid householdId,
        Guid memberId,
        bool includeArchived,
        CancellationToken cancellationToken);
}