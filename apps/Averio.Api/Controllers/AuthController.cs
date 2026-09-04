using System.Globalization;
using Averio.Application.Identity.GetCurrentUser;
using Averio.Application.Identity.Login;
using Averio.Application.Identity.RegisterUser;
using Averio.Application.Identity.ResendVerification;
using Averio.Application.Identity.VerifyEmail;
using Averio.Contracts.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    RegisterUserHandler registerUserHandler,
    LoginUserHandler loginUserHandler,
    VerifyEmailHandler verifyEmailHandler,
    ResendEmailVerificationHandler resendHandler,
    GetCurrentUserHandler currentUserHandler,
    Application.Identity.IUserSessionService
        userSessionService)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<RegisterUserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RegisterUserResponse>> Register(
            RegisterUserRequest request,
            CancellationToken cancellationToken)
    {
        var result =
            await registerUserHandler.HandleAsync(
                new RegisterUserCommand(
                    request.Email,
                    request.Password,
                    CultureInfo.CurrentUICulture.Name),
                cancellationToken);

        var response =
            new RegisterUserResponse(
                result.UserId,
                EmailVerificationRequired: true,
                result.VerificationEmailSent);

        return Created("/api/v1/auth/me", response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Login(
        LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        await loginUserHandler.HandleAsync(
            new LoginUserCommand(
                request.Email,
                request.Password,
                request.RememberMe),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await userSessionService.SignOutAsync(cancellationToken);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("email-verification")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        await verifyEmailHandler.HandleAsync(
            new VerifyEmailCommand(
                request.UserId,
                request.Token),
            cancellationToken);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("email-verification/resend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendVerification(
            ResendEmailVerificationRequest request,
            CancellationToken cancellationToken)
    {
        await resendHandler.HandleAsync(
            new ResendEmailVerificationCommand(
                request.Email),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(
            CancellationToken cancellationToken)
    {
        var result = await currentUserHandler.HandleAsync(cancellationToken);

        return Ok(
            new CurrentUserResponse(
                result.UserId,
                result.Email,
                result.EmailVerified,
                new HouseholdMembershipResponse(
                    result.HouseholdId,
                    result.HouseholdRole
                        .ToString()
                        .ToLowerInvariant())));
    }
}