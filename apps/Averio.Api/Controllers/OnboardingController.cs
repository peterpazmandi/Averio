using Averio.Application.Onboarding.CompleteOnboarding;
using Averio.Application.Onboarding.GetOnboardingStatus;
using Averio.Contracts.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Averio.Application.Onboarding.SaveOnboardingProfile;
using Averio.Application.Onboarding.SaveOnboardingHousehold;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/onboarding")]
public sealed class OnboardingController(
    GetOnboardingStatusHandler statusHandler,
    CompleteOnboardingHandler completeHandler,
    SaveOnboardingProfileHandler profileHandler,
    SaveOnboardingHouseholdHandler householdHandler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OnboardingStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var result = await statusHandler.HandleAsync(cancellationToken);

        return Ok(new OnboardingStatusResponse(
            result.Completed,
            result.CurrentStep,
            result.DisplayName,
            new HouseholdSetupResponse(
                result.HouseholdId,
                result.HouseholdName,
                result.CountryCode,
                result.PreferredCurrency)));
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Complete(CompleteOnboardingRequest request, CancellationToken cancellationToken)
    {
        await completeHandler.HandleAsync(
            new CompleteOnboardingCommand(
                request.DisplayName,
                request.HouseholdName,
                request.CountryCode,
                request.PreferredCurrency),
            cancellationToken);

        return NoContent();
    }

    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveProfile(
        SaveOnboardingProfileRequest request,
        CancellationToken cancellationToken)
    {
        await profileHandler.HandleAsync(
            new SaveOnboardingProfileCommand(
                request.DisplayName),
            cancellationToken);

        return NoContent();
    }

    [HttpPut("household")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveHousehold(
        SaveOnboardingHouseholdRequest request,
        CancellationToken cancellationToken)
    {
        await householdHandler.HandleAsync(
            new SaveOnboardingHouseholdCommand(
                request.HouseholdName,
                request.CountryCode,
                request.PreferredCurrency),
            cancellationToken);

        return NoContent();
    }
}