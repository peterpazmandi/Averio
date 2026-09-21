using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Domain.Transactions;

namespace Averio.Application.Transactions.VoidTransaction;

public sealed class VoidTransactionHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    ITransactionAggregateRepository aggregateRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(
        Guid householdId,
        Guid transactionId,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByIdForUserAsync(
            householdId,
            userId,
            cancellationToken)
            ?? throw new NotFoundException("household", householdId);

        var membership = household.Members.Single(member => member.UserId == userId);
        var transaction = await transactionRepository.GetVisibleByIdAsync(
            household.Id,
            membership.Id,
            transactionId,
            cancellationToken)
            ?? throw new NotFoundException("transaction", transactionId);

        if (transaction.UpdatedAt != expectedUpdatedAt)
        {
            throw new ConflictException(
                "transaction_changed",
                "Error_TransactionChanged");
        }

        if (transaction.Status == TransactionStatus.Voided)
        {
            return;
        }

        var account = await accountRepository.GetVisibleByIdAsync(
            household.Id,
            membership.Id,
            transaction.AccountId,
            cancellationToken)
            ?? throw new NotFoundException("account", transaction.AccountId);

        if (!TransactionAccess.CanModify(account, membership))
        {
            throw new ForbiddenException();
        }

        await using var databaseTransaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            if (TransactionAccess.ContributesToHouseholdCashFlow(transaction))
            {
                await aggregateRepository.ApplyDeltaAsync(
                    transaction.HouseholdId,
                    transaction.AccountId,
                    transaction.Currency.Value,
                    transaction.OccurredOn,
                    transaction.Kind,
                    -transaction.Amount,
                    -1,
                    cancellationToken);
            }

            transaction.Void(timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
