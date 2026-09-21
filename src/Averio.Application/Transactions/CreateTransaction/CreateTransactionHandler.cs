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
    IReadOnlyList<TransactionLabelInput> Labels);

public sealed class CreateTransactionHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    ITransactionAggregateRepository aggregateRepository,
    IValidator<TransactionInput> validator,
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
            command.Labels);

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
            command.Items
                .Select(item => new TransactionItemDraft(
                    item.Description,
                    item.CategoryName,
                    item.Amount))
                .ToArray(),
            command.Labels
                .Select(label => new TransactionLabelDraft(
                    label.Name,
                    label.Scope))
                .ToArray(),
            timeProvider.GetUtcNow());

        await using var databaseTransaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            transactionRepository.Add(transaction);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (TransactionAccess.ContributesToHouseholdCashFlow(transaction))
            {
                await aggregateRepository.ApplyDeltaAsync(
                    transaction.HouseholdId,
                    transaction.AccountId,
                    transaction.Currency.Value,
                    transaction.OccurredOn,
                    transaction.Kind,
                    transaction.Amount,
                    1,
                    cancellationToken);
            }

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
