using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Identity;
using FluentValidation;

namespace Averio.Application.Onboarding.SaveOnboardingProfile;

public sealed class SaveOnboardingProfileHandler(
    IValidator<SaveOnboardingProfileCommand> validator,
    ICurrentUser currentUser,
    IIdentityService identityService)
{
    public async Task HandleAsync(SaveOnboardingProfileCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var updated = await identityService.UpdateDisplayNameAsync(
            userId,
            command.DisplayName.Trim(),
            cancellationToken);

        if (!updated)
        {
            throw new AuthenticationFailedException();
        }
    }
}