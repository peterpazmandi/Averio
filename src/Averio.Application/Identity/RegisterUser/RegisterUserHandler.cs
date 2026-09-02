using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Households;
using FluentValidation;

namespace Averio.Application.Identity.RegisterUser;

public sealed class RegisterUserHandler(
    IValidator<RegisterUserCommand> validator,
    IIdentityService identityService,
    IEmailVerificationSender emailVerificationSender,
    IHouseholdRepository householdRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<RegisterUserResult> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedEmail = command.Email.Trim();

        var existingUser = await identityService.FindByEmailAsync(normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            throw new ConflictException("email_already_registered", ErrorResourceKeys.EmailAlreadyRegistered);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var creationResult = await identityService.CreateUserAsync(
                                                    normalizedEmail,
                                                    command.Password,
                                                    command.Culture,
                                                    cancellationToken);

        if (!creationResult.Succeeded || creationResult.UserId is null)
        {
            if (creationResult.ErrorCodes.Contains(IdentityCreationErrorCodes.DuplicateEmail))
            {
                throw new ConflictException("email_already_registered", ErrorResourceKeys.EmailAlreadyRegistered);
            }

            throw new BusinessRuleException("identity_creation_failed", ErrorResourceKeys.IdentityCreationFailed);
        }

        var userId = creationResult.UserId.Value;

        var household = Household.CreateForUser(userId, timeProvider.GetUtcNow());

        householdRepository.Add(household);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        /*
         * Email is deliberately sent AFTER the database
         * transaction commits.
         *
         * We don't want an external Brevo call inside a DB
         * transaction.
         *
         * If sending fails, the account remains valid but
         * unverified and the user can resend the email.
         */

        var verificationToken = await identityService
                .GenerateEmailVerificationTokenAsync(userId, cancellationToken);

        var emailSent =
            verificationToken is not null
            && await emailVerificationSender.TrySendAsync(
                userId,
                normalizedEmail,
                verificationToken,
                command.Culture,
                cancellationToken);

        return new RegisterUserResult(userId, emailSent);
    }
}