using System.Security.Claims;
using Averio.Contracts.Profile;
using Averio.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/profile")]
public sealed class ProfileController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    private const int MaxPictureUploadBytes = 5 * 1024 * 1024;
    private const int MaxPictureStoredBytes = 256 * 1024;

    [HttpGet]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken ct)
    {
        PreventResponseCaching();
        var user = await GetUserAsync(ct);
        return Ok(new ProfileResponse(user.Id, user.DisplayName ?? string.Empty, user.Email ?? string.Empty,
            user.EmailConfirmed, NormalizeCulture(user.PreferredCulture), user.TwoFactorEnabled, user.ProfilePicture is not null));
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await GetUserAsync(ct);
        var name = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
            return BadRequest(new { code = "profile_name_invalid" });
        var culture = request.PreferredCulture?.Trim();
        if (culture is not ("en-US" or "de-DE" or "hu-HU"))
            return BadRequest(new { code = "profile_culture_invalid" });
        user.DisplayName = name;
        user.PreferredCulture = culture;
        var result = await users.UpdateAsync(user);
        return result.Succeeded ? NoContent() : IdentityError(result);
    }

    [HttpPost("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await GetUserAsync(ct);
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length > 256)
            return BadRequest(new { code = "password_invalid" });
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword ?? string.Empty, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { code = "password_update_failed" });
        await signInManager.RefreshSignInAsync(user);
        return NoContent();
    }

    [HttpPost("sessions/revoke-other-devices")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeOtherDevices(PasswordConfirmationRequest request, CancellationToken ct)
    {
        var user = await GetUserAsync(ct);
        if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty))
            return Unauthorized();
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
            return IdentityError(result);
        await signInManager.RefreshSignInAsync(user);
        return NoContent();
    }

    [HttpGet("picture")]
    [Produces("image/jpeg")]
    public async Task<IActionResult> GetPicture(CancellationToken ct)
    {
        PreventResponseCaching();
        var user = await GetUserAsync(ct);
        return user.ProfilePicture is null ? NotFound() : File(user.ProfilePicture, "image/jpeg", enableRangeProcessing: false);
    }

    [HttpPut("picture")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [RequestSizeLimit(MaxPictureUploadBytes + 64 * 1024)]
    public async Task<IActionResult> SetPicture(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > MaxPictureUploadBytes ||
            file.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            return BadRequest(new { code = "profile_picture_invalid" });

        await using var input = file.OpenReadStream();
        using var codec = SKCodec.Create(input);
        if (codec is null || codec.Info.Width > 6000 || codec.Info.Height > 6000 ||
            codec.Info.Width * (long)codec.Info.Height > 20_000_000)
            return BadRequest(new { code = "profile_picture_invalid" });
        input.Position = 0;
        using var source = SKBitmap.Decode(input);
        if (source is null)
            return BadRequest(new { code = "profile_picture_invalid" });

        var side = Math.Min(source.Width, source.Height);
        var sourceRect = new SKRectI((source.Width - side) / 2, (source.Height - side) / 2,
            (source.Width + side) / 2, (source.Height + side) / 2);
        using var destination = new SKBitmap(256, 256, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(destination))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(source, sourceRect, new SKRect(0, 0, 256, 256),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        using var image = SKImage.FromBitmap(destination);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 84);
        if (encoded.Size > MaxPictureStoredBytes)
            return BadRequest(new { code = "profile_picture_too_large" });

        var user = await GetUserAsync(ct);
        user.ProfilePicture = encoded.ToArray();
        var result = await users.UpdateAsync(user);
        return result.Succeeded ? NoContent() : IdentityError(result);
    }

    [HttpDelete("picture")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePicture(CancellationToken ct)
    {
        var user = await GetUserAsync(ct);
        user.ProfilePicture = null;
        var result = await users.UpdateAsync(user);
        return result.Succeeded ? NoContent() : IdentityError(result);
    }

    [HttpPost("authenticator/setup")]
    [ProducesResponseType<AuthenticatorSetupResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticatorSetupResponse>> StartAuthenticatorSetup(PasswordConfirmationRequest request, CancellationToken ct)
    {
        PreventResponseCaching();
        var user = await GetUserAsync(ct);
        if (user.TwoFactorEnabled)
            return Conflict(new { code = "mfa_already_enabled" });
        if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty))
            return Unauthorized();
        var result = await users.ResetAuthenticatorKeyAsync(user);
        if (!result.Succeeded)
            return IdentityError(result);
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
            return Problem("Could not create an authenticator secret.");
        var label = $"Averio:{Uri.EscapeDataString(user.Email ?? string.Empty)}";
        var uri = $"otpauth://totp/{label}?secret={key}&issuer=Averio&digits=6&period=30";
        return Ok(new AuthenticatorSetupResponse(key, uri));
    }

    [HttpPost("authenticator/enable")]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecoveryCodesResponse>> EnableAuthenticator(EnableAuthenticatorRequest request, CancellationToken ct)
    {
        PreventResponseCaching();
        var user = await GetUserAsync(ct);
        if (user.TwoFactorEnabled)
            return Conflict(new { code = "mfa_already_enabled" });
        if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, NormalizeCode(request.Code)))
            return BadRequest(new { code = "mfa_code_invalid" });
        var enabled = await users.SetTwoFactorEnabledAsync(user, true);
        if (!enabled.Succeeded)
            return IdentityError(enabled);
        await users.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return Ok(new RecoveryCodesResponse(codes?.ToArray() ?? []));
    }

    [HttpPost("authenticator/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DisableAuthenticator(DisableAuthenticatorRequest request, CancellationToken ct)
    {
        var user = await GetUserAsync(ct);
        if (!user.TwoFactorEnabled)
            return NoContent();
        if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty))
            return Unauthorized();
        if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, NormalizeCode(request.Code)))
            return BadRequest(new { code = "mfa_code_invalid" });
        var result = await users.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
            return IdentityError(result);
        await users.ResetAuthenticatorKeyAsync(user);
        await users.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);
        return NoContent();
    }

    [HttpPost("authenticator/recovery-codes")]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecoveryCodesResponse>> RegenerateRecoveryCodes(DisableAuthenticatorRequest request, CancellationToken ct)
    {
        PreventResponseCaching();
        var user = await GetUserAsync(ct);
        if (!user.TwoFactorEnabled)
            return Conflict(new { code = "mfa_not_enabled" });
        if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty))
            return Unauthorized();
        if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, NormalizeCode(request.Code)))
            return BadRequest(new { code = "mfa_code_invalid" });
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return Ok(new RecoveryCodesResponse(codes?.ToArray() ?? []));
    }

    private async Task<ApplicationUser> GetUserAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = Guid.TryParse(userId, out var id) ? await users.FindByIdAsync(id.ToString()) : null;
        return user ?? throw new UnauthorizedAccessException();
    }

    private static string NormalizeCode(string? code) => (code ?? string.Empty).Replace(" ", "").Replace("-", "");

    private void PreventResponseCaching() => Response.Headers.CacheControl = "private, no-store";

    private static string NormalizeCulture(string? culture) => culture?.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() switch
    {
        "de" => "de-DE",
        "hu" => "hu-HU",
        _ => "en-US"
    };

    private ObjectResult IdentityError(IdentityResult result) => Problem(
        title: "The profile change could not be saved.",
        statusCode: StatusCodes.Status400BadRequest,
        extensions: new Dictionary<string, object?> { ["code"] = "profile_update_failed" });
}
