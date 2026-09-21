using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Transactions;
using FluentValidation;

namespace Averio.Application.Transactions.UpdateTransaction;

public sealed record UpdateTransactionCommand(
    Guid HouseholdId,
    Guid TransactionId,
    Guid AccountId,
    TransactionKind Kind,
    TransactionVisibility Visibility,
    decimal Amount,
    string Description,
    string? Note,
    DateOnly OccurredOn,
    DateTimeOffset ExpectedUpdatedAt);

public sealed class UpdateTransactionHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    ITransactionAggregateRepository aggregateRepository,
    IValidator<TransactionInput> validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(
        UpdateTransactionCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(
            new TransactionInput(
                command.AccountId,
                command.Kind,
                command.Visibility,
                command.Amount,
                command.Description,
                command.Note,
                command.OccurredOn),
            cancellationToken);

        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByIdForUserAsync(
            command.HouseholdId,
            userId,
            cancellationToken)
            ?? throw new NotFoundException("household", command.HouseholdId);

        var membership = household.Members.Single(member => member.UserId == userId);
        var transaction = await transactionRepository.GetVisibleByIdAsync(
            household.Id,
            membership.Id,
            command.TransactionId,
            cancellationToken)
            ?? throw new NotFoundException("transaction", command.TransactionId);

        if (transaction.UpdatedAt != command.ExpectedUpdatedAt)
        {
            throw new ConflictException(
                "transaction_changed",
                "Error_TransactionChanged");
        }

        if (transaction.Status != TransactionStatus.Posted)
        {
            throw new ConflictException(
                "transaction_voided",
                "Error_TransactionVoided");
        }

        var currentAccount = await accountRepository.GetVisibleByIdAsync(
            household.Id,
            membership.Id,
            transaction.AccountId,
            cancellationToken)
            ?? throw new NotFoundException("account", transaction.AccountId);

        var targetAccount = command.AccountId == currentAccount.Id
            ? currentAccount
            : await accountRepository.GetVisibleByIdAsync(
                household.Id,
                membership.Id,
                command.AccountId,
                cancellationToken)
                ?? throw new NotFoundException("account", command.AccountId);

        if (targetAccount.Status != AccountStatus.Active)
        {
            throw new ConflictException("account_archived", "Error_AccountArchived");
        }

        if (!TransactionAccess.CanModify(currentAccount, membership)
            || !TransactionAccess.CanModify(targetAccount, membership))
        {
            throw new ForbiddenException();
        }

        if (targetAccount.Currency != transaction.Currency)
        {
            throw new BusinessRuleException(
                "transaction_currency_change_not_allowed",
                "Error_TransactionCurrencyChangeNotAllowed");
        }

        if (!TransactionAccess.IsVisibilityAllowed(targetAccount, command.Visibility))
        {
            throw new BusinessRuleException(
                "transaction_visibility_not_allowed",
                "Error_TransactionVisibilityNotAllowed");
        }

        var oldContributes = TransactionAccess.ContributesToHouseholdCashFlow(transaction);
        var oldAccountId = transaction.AccountId;
        var oldOccurredOn = transaction.OccurredOn;
        var oldKind = transaction.Kind;
        var oldAmount = transaction.Amount;
        var currency = transaction.Currency.Value;

        await using var databaseTransaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            if (oldContributes)
            {
                await aggregateRepository.ApplyDeltaAsync(
                    transaction.HouseholdId,
                    oldAccountId,
                    currency,
                    oldOccurredOn,
                    oldKind,
                    -oldAmount,
                    -1,
                    cancellationToken);
            }

            transaction.Update(
                targetAccount.Id,
                command.Kind,
                command.Visibility,
                command.Amount,
                targetAccount.Currency,
                command.Description,
                command.Note,
                command.OccurredOn,
                timeProvider.GetUtcNow());

            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (TransactionAccess.ContributesToHouseholdCashFlow(transaction))
            {
                await aggregateRepository.ApplyDeltaAsync(
                    transaction.HouseholdId,
                    transaction.AccountId,
                    currency,
                    transaction.OccurredOn,
                    transaction.Kind,
                    transaction.Amount,
                    1,
                    cancellationToken);
            }

            await databaseTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
