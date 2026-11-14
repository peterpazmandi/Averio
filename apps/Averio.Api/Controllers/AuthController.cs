using System.Globalization;
using Averio.Application.Identity.GetCurrentUser;
using Averio.Application.Identity.Login;
using Averio.Application.Identity.RegisterUser;
using Averio.Application.Identity.RequestPasswordReset;
using Averio.Application.Identity.ResendVerification;
using Averio.Application.Identity.ResetPassword;
using Averio.Application.Identity.VerifyEmail;
using Averio.Api.Security;
using Averio.Contracts.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Averio.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    RegisterUserHandler registerUserHandler,
    LoginUserHandler loginUserHandler,
    VerifyEmailHandler verifyEmailHandler,
    ResendEmailVerificationHandler resendHandler,
    GetCurrentUserHandler currentUserHandler,
    ResetPasswordHandler resetPasswordHandler,
    RequestPasswordResetHandler requestPasswordResetHandler,
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
    [ProducesResponseType<LoginResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Login(
        LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        var requiresTwoFactor = await loginUserHandler.HandleAsync(
            new LoginUserCommand(
                request.Email,
                request.Password,
                request.RememberMe),
            cancellationToken);

        return requiresTwoFactor ? Accepted(new LoginResponse(true)) : NoContent();
    }

    [AllowAnonymous]
    [HttpPost("login/two-factor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LoginWithTwoFactor(LoginTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var succeeded = request.RecoveryCode is { Length: > 0 }
            ? await userSessionService.CompleteRecoveryCodeSignInAsync(request.RecoveryCode, request.RememberMe, cancellationToken)
            : await userSessionService.CompleteTwoFactorSignInAsync(request.Code ?? string.Empty, request.RememberMe, cancellationToken);
        if (!succeeded)
            return Unauthorized();
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
    [EnableRateLimiting(RateLimiterPolicies.PasswordResetRequests)]
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

    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicies.PasswordResetRequests)]
    [HttpPost("password-reset-requests")]
    [ProducesResponseType(
    StatusCodes.Status204NoContent)]
    public async Task<IActionResult>
    RequestPasswordReset(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        await requestPasswordResetHandler
            .HandleAsync(
                new RequestPasswordResetCommand(
                    request.Email),
                cancellationToken);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("password-resets")]
    [ProducesResponseType(
        StatusCodes.Status204NoContent)]
    public async Task<IActionResult>
        ResetPassword(
            ResetPasswordRequest request,
            CancellationToken cancellationToken)
    {
        await resetPasswordHandler
            .HandleAsync(
                new ResetPasswordCommand(
                    request.UserId,
                    request.Token,
                    request.Password),
                cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var result = await currentUserHandler.HandleAsync(cancellationToken);

        return Ok(new CurrentUserResponse(
                result.UserId,
                result.Email,
                result.DisplayName,
                result.EmailVerified,
                result.OnboardingRequired,
                new HouseholdMembershipResponse(
                    result.HouseholdId,
                    result.HouseholdRole
                        .ToString()
                        .ToLowerInvariant())));
    }
}
