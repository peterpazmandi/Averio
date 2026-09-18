using Averio.Application.Accounts.Common;
using Averio.Application.Accounts.CreateAccount;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Households;

namespace Averio.Application.Onboarding.CompleteFirstAccount;

public sealed class CompleteFirstAccountHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    AccountCreator accountCreator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(
                cancellationToken);

        var household =
            await householdRepository.GetByIdForUserForUpdateAsync(
                command.HouseholdId,
                userId,
                cancellationToken)
            ?? throw new NotFoundException(
                "household",
                command.HouseholdId);

        var membership = household.Members.Single(
            member => member.UserId == userId);

        if (membership.Role != HouseholdRole.Owner)
        {
            throw new ForbiddenException();
        }

        if (household.IsFinancialSetupCompleted)
        {
            await transaction.CommitAsync(cancellationToken);

            return;
        }

        if (!household.IsOnboardingCompleted)
        {
            throw new OnboardingRequiredException();
        }

        await accountCreator.CreateAsync(
            command,
            household,
            membership,
            cancellationToken);

        household.CompleteFinancialSetup(timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
