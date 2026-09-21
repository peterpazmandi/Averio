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
    decimal Amount,
    string Description,
    string? Note,
    DateOnly OccurredOn);

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

        if (!household.IsFinancialSetupCompleted)
        {
            throw new OnboardingRequiredException();
        }

        var membership = household.Members.Single(member => member.UserId == userId);
        var account = await accountRepository.GetVisibleByIdAsync(
            household.Id,
            membership.Id,
            command.AccountId,
            cancellationToken)
            ?? throw new NotFoundException("account", command.AccountId);

        if (account.Status != AccountStatus.Active)
        {
            throw new ConflictException("account_archived", "Error_AccountArchived");
        }

        if (!TransactionAccess.CanModify(account, membership))
        {
            throw new ForbiddenException();
        }

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
            command.Amount,
            account.Currency,
            command.Description,
            command.Note,
            command.OccurredOn,
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
