using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Domain.Transactions;

namespace Averio.Application.Transactions.ArchiveTransaction;

// Archiving is presentation-only. It never posts or reverses financial or allowance entries.
public sealed class ArchiveTransactionHandler(
    ICurrentUser user, IHouseholdRepository households, IAccountRepository accounts,
    ITransactionRepository transactions, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task HandleAsync(Guid householdId, Guid transactionId,
        bool archived, DateTimeOffset expectedUpdatedAt, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new AuthenticationFailedException();
        await using var database = await unitOfWork.BeginTransactionAsync(ct);
        var household = await households.GetByIdForUserAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        var actor = household.Members.Single(x => x.UserId == userId && x.LeftAtUtc is null);
        var transaction = await transactions.GetVisibleByIdAsync(householdId, actor.Id, transactionId, ct)
            ?? throw new NotFoundException("transaction", transactionId);
        var account = await accounts.GetVisibleByIdAsync(householdId, actor.Id, transaction.AccountId, ct)
            ?? throw new NotFoundException("account", transaction.AccountId);
        if (!TransactionAccess.CanModify(account, actor))
            throw new ForbiddenException();
        if (transaction.UpdatedAt != expectedUpdatedAt)
            throw new ConflictException("transaction_changed", "Error_TransactionChanged");
        if (transaction.Status != TransactionStatus.Posted)
            throw new ConflictException("transaction_voided", "Error_TransactionVoided");
        transaction.SetArchived(archived, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        await database.CommitAsync(ct);
    }
}
