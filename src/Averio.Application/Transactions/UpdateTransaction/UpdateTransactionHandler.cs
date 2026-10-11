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
    string? Merchant,
    string? Note,
    DateOnly OccurredOn,
    IReadOnlyList<TransactionItemInput> Items,
    IReadOnlyList<Guid> LabelIds,
    DateTimeOffset ExpectedUpdatedAt,
    Averio.Application.Activities.ActivityAssignmentInput? ActivityAssignment = null);

public sealed class UpdateTransactionHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IValidator<TransactionInput> validator,
    TransactionReferenceResolver referenceResolver,
    TransactionAggregateWriter aggregateWriter,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    Averio.Application.Activities.ActivityHandler activities)
{
    public async Task HandleAsync(
        UpdateTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var input = new TransactionInput(
            command.AccountId,
            command.Kind,
            command.Visibility,
            command.Merchant,
            command.Note,
            command.OccurredOn,
            command.Items,
            command.LabelIds);

        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        await using var databaseTransaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

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
            throw new ConflictException("account_archived", "Error_AccountArchived");

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



        var items = await referenceResolver.ResolveItemsAsync(
            household.Id,
            command.Kind,
            command.Items,
            cancellationToken, transaction.Items);

        var labelIds = await referenceResolver.ResolveLabelIdsAsync(
            household.Id,
            targetAccount.Id,
            command.LabelIds,
            cancellationToken);

        try
        {
            await activities.AssignWithinTransactionAsync(household.Id, membership.Id, transaction.Id, command.ActivityAssignment, cancellationToken);
            await aggregateWriter.ApplyAsync(transaction, -1, cancellationToken);

            transaction.Update(
                targetAccount.Id,
                command.Kind,
                command.Visibility,
                targetAccount.Currency,
                command.Merchant,
                command.Note,
                command.OccurredOn,
                items,
                labelIds,
                timeProvider.GetUtcNow());

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await aggregateWriter.ApplyAsync(transaction, 1, cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}


