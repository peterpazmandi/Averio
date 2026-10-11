using Averio.Application.Accounts.Common;
using Averio.Application.Accounts.CreateAccount;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Accounts;
using FluentValidation;
using FluentValidation.Results;

namespace Averio.Application.Accounts.UpdateAccount;

public sealed class UpdateAccountHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    IValidator<CreateAccountCommand> validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(UpdateAccountCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        await using var writeTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var household = await householdRepository.GetByIdForUserAsync(
            command.HouseholdId, userId, cancellationToken)
            ?? throw new NotFoundException("household", command.HouseholdId);
        var membership = household.Members.Single(member => member.UserId == userId);
        var account = await accountRepository.GetVisibleByIdAsync(
            household.Id, membership.Id, command.AccountId, cancellationToken)
            ?? throw new NotFoundException("account", command.AccountId);
        if (!AccountAccess.CanModify(account, membership)) throw new ForbiddenException();
        if (account.Status == AccountStatus.Archived)
            throw new ConflictException("account_archived", "Error_AccountArchived");
        if (account.UpdatedAt != command.ExpectedUpdatedAt)
            throw new ConflictException("account_changed", "Error_AccountChanged");

        // The same validation as creation; immutable values come from the stored account.
        await validator.ValidateAndThrowAsync(new CreateAccountCommand(
            household.Id, command.Name, command.Type, account.Scope,
            command.OwnerHouseholdMemberIds, command.Visibility,
            command.IncludeInHouseholdReports, account.Currency.Value,
            account.BalanceAnchorAmount, account.BalanceAnchorDate), cancellationToken);
        var members = household.Members.Select(member => member.Id).ToHashSet();
        if (command.OwnerHouseholdMemberIds.Any(id => !members.Contains(id)))
            throw OwnerValidation("Validation_AccountOwnerOutsideHousehold");
        if (account.Scope == AccountScope.Personal
            && !command.OwnerHouseholdMemberIds.Contains(membership.Id))
            throw OwnerValidation("Validation_PersonalAccountMustIncludeYou");

        account.Update(command.Name, command.Type, command.OwnerHouseholdMemberIds,
            command.Visibility, command.IncludeInHouseholdReports, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await writeTransaction.CommitAsync(cancellationToken);
    }

    private static ValidationException OwnerValidation(string code) => new(
        [new ValidationFailure("OwnerHouseholdMemberIds", code) { ErrorCode = code }]);
}

