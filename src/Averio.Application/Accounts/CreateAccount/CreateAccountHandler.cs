using Averio.Application.Accounts.Common;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Accounts.CreateAccount;

public sealed class CreateAccountHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    AccountCreator accountCreator,
    IUnitOfWork unitOfWork)
{
    public async Task<Guid> HandleAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        await using var writeTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var household =
            await householdRepository.GetByIdForUserAsync(
                command.HouseholdId,
                userId,
                cancellationToken)
            ?? throw new NotFoundException(
                "household",
                command.HouseholdId);

        if (!household.IsFinancialSetupCompleted)
        {
            throw new OnboardingRequiredException();
        }

        var membership = household.Members.Single(
            member => member.UserId == userId);

        var account = await accountCreator.CreateAsync(
            command,
            household,
            membership,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await writeTransaction.CommitAsync(cancellationToken);

        return account.Id;
    }
}

