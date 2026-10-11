using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Identity;
using Averio.Domain.Common;
using Averio.Domain.Households;
using FluentValidation;

namespace Averio.Application.Onboarding.CompleteOnboarding;

public sealed class CompleteOnboardingHandler(
    IValidator<CompleteOnboardingCommand> validator,
    ICurrentUser currentUser,
    IIdentityService identityService,
    IHouseholdRepository householdRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    Averio.Application.Households.Invitations.InvitationHandler invitations,
    Averio.Application.Households.Invitations.IInvitationRepository invitationRepository)
{
    public async Task HandleAsync(
        CompleteOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var userId =
            currentUser.UserId
            ?? throw new AuthenticationFailedException();
        if ((await invitations.PendingAsync(cancellationToken)).Count > 0)
            throw new ConflictException("invitation_onboarding", "Error_InvitationOnboarding");


        await using var transaction =
            await unitOfWork.BeginTransactionAsync(
                cancellationToken);

        // Match acceptance's identity-before-household order when both rows are written.
        await invitationRepository.LockUserAsync(userId, cancellationToken);

        var household =
            await householdRepository.GetByUserIdAsync(
                userId,
                cancellationToken)
            ?? throw new HouseholdRequiredException();

        if (household.IsFinancialSetupCompleted)
            throw new ConflictException("household_setup_completed", "Error_HouseholdSetupCompleted");

        var membership = household.Members.Single(
            member => member.UserId == userId);

        if (membership.Role != HouseholdRole.Owner)
        {
            throw new ForbiddenException();
        }

        var currency = CurrencyCode.From(command.PreferredCurrency);



        var userUpdated =
            await identityService.UpdateDisplayNameAsync(
                userId,
                command.DisplayName,
                cancellationToken);

        if (!userUpdated)
        {
            throw new AuthenticationFailedException();
        }

        household.CompleteOnboarding(
            command.HouseholdName,
            command.CountryCode,
            currency,
            timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
