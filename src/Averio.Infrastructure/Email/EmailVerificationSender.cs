using System.Globalization;
using System.Text.Encodings.Web;
using Averio.Application.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Averio.Infrastructure.Email;

internal sealed class EmailVerificationSender(
    ITransactionalEmailSender transactionalEmailSender,
    IStringLocalizer<EmailMessages> localizer,
    IOptions<EmailOptions> options,
    ILogger<EmailVerificationSender> logger)
    : IEmailVerificationSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task<bool> TrySendAsync(
        Guid userId,
        string email,
        string verificationToken,
        string culture,
        CancellationToken cancellationToken)
    {
        try
        {
            var cultureInfo = GetCulture(culture);
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;

                var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');

                var verificationUrl =
                    $"{baseUrl}/verify-email"
                    + $"?userId={userId:D}"
                    + $"&token={Uri.EscapeDataString(verificationToken)}";

                var encodedUrl = HtmlEncoder.Default.Encode(verificationUrl);

                var subject = localizer["Verification_Subject"].Value;
                var heading = HtmlEncoder.Default.Encode(localizer["Verification_Heading"].Value);
                var description = HtmlEncoder.Default.Encode(localizer["Verification_Description"].Value);
                var button = HtmlEncoder.Default.Encode(localizer["Verification_Button"].Value);
                var footer = HtmlEncoder.Default.Encode(localizer["Verification_Footer"].Value);

                var html = $$"""
                <!doctype html>
                <html>
                <body style="font-family:Arial,sans-serif;background:#f8fafc;padding:32px;">
                    <div style="max-width:560px;margin:0 auto;background:white;padding:32px;border-radius:16px;">
                        <h1 style="margin-top:0;">{{heading}}</h1>
                        <p>{{description}}</p>
                        <p style="margin:32px 0;">
                            <a href="{{encodedUrl}}"
                               style="display:inline-block;padding:12px 20px;background:#0f172a;color:white;text-decoration:none;border-radius:8px;">
                                {{button}}
                            </a>
                        </p>
                        <p style="font-size:13px;color:#64748b;">{{footer}}</p>
                    </div>
                </body>
                </html>
                """;

                await transactionalEmailSender.SendAsync(
                    email,
                    subject,
                    html,
                    cancellationToken);

                return true;
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not send verification email to user {UserId}",
                userId);

            return false;
        }
    }

    private static CultureInfo GetCulture(string culture)
    {
        return culture switch
        {
            "de-DE" => CultureInfo.GetCultureInfo("de-DE"),
            "hu-HU" => CultureInfo.GetCultureInfo("hu-HU"),
            _ => CultureInfo.GetCultureInfo("en-US")
        };
    }
}