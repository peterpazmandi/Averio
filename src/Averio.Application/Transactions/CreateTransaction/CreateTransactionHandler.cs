using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Transactions;
using FluentValidation;

namespace Averio.Application.Transactions.CreateTransaction;

public sealed record CreateTransactionCommand(
    Guid HouseholdId,
    Guid AccountId,
    TransactionKind Kind,
    TransactionVisibility Visibility,
    string? Merchant,
    string? Note,
    DateOnly OccurredOn,
    IReadOnlyList<TransactionItemInput> Items,
    IReadOnlyList<Guid> LabelIds);

public sealed class CreateTransactionHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IValidator<TransactionInput> validator,
    TransactionReferenceResolver referenceResolver,
    TransactionAggregateWriter aggregateWriter,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> HandleAsync(
        CreateTransactionCommand command,
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

        var household = await householdRepository.GetByIdForUserAsync(
            command.HouseholdId,
            userId,
            cancellationToken)
            ?? throw new NotFoundException("household", command.HouseholdId);

        if (!household.IsFinancialSetupCompleted)
            throw new OnboardingRequiredException();

        var membership = household.Members.Single(member => member.UserId == userId);
        var account = await accountRepository.GetVisibleByIdAsync(
            household.Id,
            membership.Id,
            command.AccountId,
            cancellationToken)
            ?? throw new NotFoundException("account", command.AccountId);

        if (account.Status != AccountStatus.Active)
            throw new ConflictException("account_archived", "Error_AccountArchived");

        if (!TransactionAccess.CanModify(account, membership))
            throw new ForbiddenException();

        if (!TransactionAccess.IsVisibilityAllowed(account, command.Visibility))
        {
            throw new BusinessRuleException(
                "transaction_visibility_not_allowed",
                "Error_TransactionVisibilityNotAllowed");
        }

        await using var databaseTransaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var items = await referenceResolver.ResolveItemsAsync(
            household.Id,
            command.Kind,
            command.Items,
            cancellationToken);

        var labelIds = await referenceResolver.ResolveLabelIdsAsync(
            household.Id,
            account.Id,
            command.LabelIds,
            cancellationToken);

        var transaction = Transaction.Create(
            household.Id,
            account.Id,
            membership.Id,
            command.Kind,
            command.Visibility,
            account.Currency,
            command.Merchant,
            command.Note,
            command.OccurredOn,
            items,
            labelIds,
            timeProvider.GetUtcNow());

        try
        {
            transactionRepository.Add(transaction);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await aggregateWriter.ApplyAsync(transaction, 1, cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
            return transaction.Id;
        }
        catch
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
