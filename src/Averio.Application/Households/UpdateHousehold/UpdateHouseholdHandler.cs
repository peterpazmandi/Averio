using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Common;
using Averio.Domain.Households;
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

        var membership = household.Members.Single(member => member.UserId == userId);
        if (membership.Role != HouseholdRole.Owner)
            throw new ForbiddenException();

        if (!household.IsOnboardingCompleted)
        {
            throw new OnboardingRequiredException();
        }

        if (command.ExpectedVersion != household.Version)
            throw new ConflictException("household_changed", "Error_HouseholdChanged");

        var name = command.Name ?? household.Name!;

        var country = command.CountryCode ?? household.CountryCode!;

        var currency = command.PreferredCurrency is null
                ? household.ReportingCurrency!.Value
                : CurrencyCode.From(command.PreferredCurrency);

        household.UpdateProfile(name, country, currency);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}