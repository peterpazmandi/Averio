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
    IReadOnlyList<Guid> LabelIds,
    Averio.Application.Activities.ActivityAssignmentInput? ActivityAssignment = null,
    bool ChargePersonalMoney = false);

public sealed class CreateTransactionHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IValidator<TransactionInput> validator,
    TransactionReferenceResolver referenceResolver,
    TransactionAggregateWriter aggregateWriter,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    Averio.Application.Activities.ActivityHandler activities,
    Averio.Application.PersonalMoney.PersonalMoneyHandler personalMoney)
{
    public async Task<Guid> HandleAsync(
        CreateTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var id = await CreateWithinTransactionAsync(command, userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    // Internal application entry point: callers own the transaction; authorization still runs below.
    internal async Task<Guid> CreateWithinTransactionAsync(CreateTransactionCommand command, Guid userId, CancellationToken cancellationToken)
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

        transactionRepository.Add(transaction);
        await activities.AssignWithinTransactionAsync(household.Id, membership.Id, transaction.Id, command.ActivityAssignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await aggregateWriter.ApplyAsync(transaction, 1, cancellationToken);
        if (command.ChargePersonalMoney && command.Items.Any(i => i.PersonalMoneyAmount is > 0))
            throw new BusinessRuleException("personal_money_charge_not_allowed", "Error_PersonalMoneyChargeNotAllowed");
        if (command.ChargePersonalMoney)
            await personalMoney.ChargeWithinTransactionAsync(household.Id, transaction.Id,
                transaction.Amount, transaction.Id, cancellationToken);
        await personalMoney.ApplyItemChargesWithinTransactionAsync(household.Id, transaction.Id,
            command.Items, cancellationToken);
        return transaction.Id;
    }
}


