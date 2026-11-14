using Averio.Api.Security;
using Averio.Contracts.Profile;
using Averio.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace Averio.Api.Controllers;

[ApiController]
public sealed class PasskeysController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    private const int MaxPasskeys = 10;

    [Authorize]
    [HttpGet("api/v1/profile/passkeys")]
    [ProducesResponseType<PasskeyResponse[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PasskeyResponse[]>> List()
    {
        NoStore();
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var keys = await users.GetPasskeysAsync(user);
        return Ok(keys.Select(key => new PasskeyResponse(
            WebEncoders.Base64UrlEncode(key.CredentialId), key.Name ?? string.Empty, key.CreatedAt)).ToArray());
    }

    [Authorize]
    [HttpPost("api/v1/profile/passkeys/options")]
    [Produces("application/json")]
    public async Task<IActionResult> CreationOptions(PasskeyCreationOptionsRequest request)
    {
        NoStore();
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty)) return Unauthorized();
        if ((await users.GetPasskeysAsync(user)).Count >= MaxPasskeys)
            return Conflict(new { code = "passkey_limit_reached" });

        var options = await signInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = user.Id.ToString(),
            Name = user.Email ?? string.Empty,
            DisplayName = user.DisplayName ?? user.Email ?? string.Empty
        });
        return Content(options, "application/json");
    }

    [Authorize]
    [HttpPost("api/v1/profile/passkeys")]
    [RequestSizeLimit(128 * 1024)]
    [ProducesResponseType<PasskeyResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PasskeyResponse>> Register(RegisterPasskeyRequest request)
    {
        NoStore();
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 64 ||
            string.IsNullOrEmpty(request.CredentialJson) || request.CredentialJson.Length > 96 * 1024)
            return BadRequest(new { code = "passkey_invalid" });
        if ((await users.GetPasskeysAsync(user)).Count >= MaxPasskeys)
            return Conflict(new { code = "passkey_limit_reached" });

        try
        {
            var attestation = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
            if (!attestation.Succeeded || !string.Equals(attestation.UserEntity?.Id, user.Id.ToString(), StringComparison.Ordinal))
                return BadRequest(new { code = "passkey_invalid" });

            var passkey = attestation.Passkey!;
            passkey.Name = name;
            var result = await users.AddOrUpdatePasskeyAsync(user, passkey);
            if (!result.Succeeded) return BadRequest(new { code = "passkey_invalid" });

            return Created("/api/v1/profile/passkeys", new PasskeyResponse(
                WebEncoders.Base64UrlEncode(passkey.CredentialId), passkey.Name, passkey.CreatedAt));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { code = "passkey_expired" });
        }
    }

    [Authorize]
    [HttpDelete("api/v1/profile/passkeys/{credentialId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(string credentialId, RemovePasskeyRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty)) return Unauthorized();

        byte[] id;
        try { id = WebEncoders.Base64UrlDecode(credentialId); }
        catch (FormatException) { return BadRequest(new { code = "passkey_invalid" }); }
        if (id.Length is 0 or > 1023 || await users.GetPasskeyAsync(user, id) is null)
            return NotFound();

        var result = await users.RemovePasskeyAsync(user, id);
        return result.Succeeded ? NoContent() : BadRequest(new { code = "passkey_remove_failed" });
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicies.PasskeyAttempts)]
    [HttpPost("api/v1/auth/passkey/options")]
    [Produces("application/json")]
    public async Task<IActionResult> SignInOptions()
    {
        NoStore();
        // Discoverable credentials avoid a user-existence lookup and allow an
        // account picker on devices with more than one Averio passkey.
        var options = await signInManager.MakePasskeyRequestOptionsAsync(null);
        return Content(options, "application/json");
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicies.PasskeyAttempts)]
    [HttpPost("api/v1/auth/passkey")]
    [RequestSizeLimit(128 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignIn(PasskeySignInRequest request)
    {
        NoStore();
        if (string.IsNullOrEmpty(request.CredentialJson) || request.CredentialJson.Length > 96 * 1024)
            return Unauthorized();

        try
        {
            // .NET 10's PasskeySignInAsync omits the account checks performed
            // by password sign-in. Verify the assertion first, then enforce
            // Averio's confirmed-email and lockout policy before issuing a cookie.
            var assertion = await signInManager.PerformPasskeyAssertionAsync(request.CredentialJson);
            if (!assertion.Succeeded || assertion.User is null || assertion.Passkey is null)
                return Unauthorized();

            var user = assertion.User;
            if (!user.EmailConfirmed || await users.IsLockedOutAsync(user))
                return Unauthorized();

            user.LastLoginAtUtc = DateTimeOffset.UtcNow;
            var saved = await users.AddOrUpdatePasskeyAsync(user, assertion.Passkey);
            if (!saved.Succeeded) return Unauthorized();
            await users.ResetAccessFailedCountAsync(user);
            await signInManager.SignInAsync(user, isPersistent: request.RememberMe);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }
    }

    private void NoStore() => Response.Headers.CacheControl = "private, no-store";
}
