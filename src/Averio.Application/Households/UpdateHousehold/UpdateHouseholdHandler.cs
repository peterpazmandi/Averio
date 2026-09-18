using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Common;
using FluentValidation;

namespace Averio.Application.Households.UpdateHousehold;

public sealed class UpdateHouseholdHandler(
    IValidator<UpdateHouseholdCommand> validator,
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(UpdateHouseholdCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository
                .GetByIdForUserAsync(
                    command.HouseholdId,
                    userId,
                    cancellationToken)
            ?? throw new NotFoundException("household", command.HouseholdId);

        if (!household.IsOnboardingCompleted)
        {
            throw new OnboardingRequiredException();
        }

        var name = command.Name ?? household.Name!;

        var country = command.CountryCode ?? household.CountryCode!;

        var currency = command.PreferredCurrency is null
                ? household.ReportingCurrency!.Value
                : CurrencyCode.From(command.PreferredCurrency);

        household.UpdateProfile(name, country, currency);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}