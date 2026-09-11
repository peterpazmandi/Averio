using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Identity;
using Averio.Domain.Common;
using FluentValidation;

namespace Averio.Application.Onboarding.CompleteOnboarding;

public sealed class CompleteOnboardingHandler(
    IValidator<CompleteOnboardingCommand> validator,
    ICurrentUser currentUser,
    IIdentityService identityService,
    IHouseholdRepository householdRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
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

        var household =
            await householdRepository.GetByUserIdAsync(
                userId,
                cancellationToken)
            ?? throw new HouseholdRequiredException();

        var currency =
            Enum.Parse<CurrencyCode>(
                command.PreferredCurrency,
                ignoreCase: true);

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(
                cancellationToken);

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

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }
}