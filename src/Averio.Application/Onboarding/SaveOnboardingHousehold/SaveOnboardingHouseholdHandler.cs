using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Identity;
using Averio.Domain.Common;
using Averio.Domain.Households;
using FluentValidation;

namespace Averio.Application.Onboarding.SaveOnboardingHousehold;

public sealed class SaveOnboardingHouseholdHandler(
    IValidator<SaveOnboardingHouseholdCommand> validator,
    ICurrentUser currentUser,
    IIdentityService identityService,
    IHouseholdRepository householdRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    Averio.Application.Households.Invitations.InvitationHandler invitations)
{
    public async Task HandleAsync(
        SaveOnboardingHouseholdCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();
        if ((await invitations.PendingAsync(cancellationToken)).Count > 0)
            throw new ConflictException("invitation_onboarding", "Error_InvitationOnboarding");


        await using var writeTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var household = await householdRepository.GetByUserIdAsync(
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

        var user = await identityService.FindByIdAsync(userId, cancellationToken)
            ?? throw new AuthenticationFailedException();

        if (string.IsNullOrWhiteSpace(user.DisplayName))
        {
            throw new OnboardingRequiredException();
        }

        var currency = CurrencyCode.From(command.PreferredCurrency);

        household.CompleteOnboarding(
            command.HouseholdName.Trim(),
            command.CountryCode.Trim().ToUpperInvariant(),
            currency,
            timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await writeTransaction.CommitAsync(cancellationToken);
    }
}
