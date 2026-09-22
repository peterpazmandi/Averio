using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Domain.Labels;
using FluentValidation;

namespace Averio.Application.Labels.CreateLabel;

public sealed record CreateLabelCommand(
    Guid HouseholdId,
    string Name,
    LabelScope Scope,
    Guid? AccountId);

public sealed class CreateLabelCommandValidator
    : AbstractValidator<CreateLabelCommand>
{
    public CreateLabelCommandValidator()
    {
        RuleFor(x => x.HouseholdId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(40)
            .WithErrorCode("Validation_TransactionLabelNameInvalid");

        RuleFor(x => x.Scope)
            .IsInEnum()
            .WithErrorCode("Validation_TransactionLabelScopeInvalid");

        RuleFor(x => x.AccountId)
            .NotNull()
            .When(x => x.Scope == LabelScope.Account)
            .WithErrorCode("Validation_TransactionLabelAccountRequired");

        RuleFor(x => x.AccountId)
            .Null()
            .When(x => x.Scope == LabelScope.Household)
            .WithErrorCode("Validation_TransactionLabelHouseholdAccountInvalid");
    }
}

public sealed class CreateLabelHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    ILabelRepository labelRepository,
    IValidator<CreateLabelCommand> validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> HandleAsync(
        CreateLabelCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByIdForUserAsync(
            command.HouseholdId,
            userId,
            cancellationToken)
            ?? throw new NotFoundException("household", command.HouseholdId);

        var membership = household.Members.Single(member => member.UserId == userId);

        if (command.Scope == LabelScope.Account)
        {
            var accountId = command.AccountId!.Value;
            var account = await accountRepository.GetVisibleByIdAsync(
                household.Id,
                membership.Id,
                accountId,
                cancellationToken)
                ?? throw new NotFoundException("account", accountId);

            if (!TransactionAccess.CanModify(account, membership))
                throw new ForbiddenException();
        }

        var normalizedName = Label.Normalize(command.Name);
        var existing = await labelRepository.FindByNormalizedNameAsync(
            household.Id,
            command.AccountId,
            command.Scope,
            normalizedName,
            cancellationToken);

        if (existing is not null)
            return existing.Id;

        var label = Label.Create(
            household.Id,
            command.AccountId,
            command.Scope,
            command.Name,
            timeProvider.GetUtcNow());

        labelRepository.Add(label);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return label.Id;
    }
}
