using System.Globalization;
using System.Text.Encodings.Web;
using Averio.Application.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Averio.Infrastructure.Email;

internal sealed class PasswordResetEmailSender(
    ITransactionalEmailSender transactionalEmailSender,
    IStringLocalizer<EmailMessages> localizer,
    IOptions<EmailOptions> options,
    ILogger<PasswordResetEmailSender> logger) : IPasswordResetSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task<bool> TrySendAsync(
        Guid userId,
        string email,
        string resetToken,
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

                var resetUrl =
                    $"{baseUrl}/reset-password"
                    + $"?userId={userId:D}"
                    + $"&token={Uri.EscapeDataString(resetToken)}";

                /*
                 * The URL will be inserted into HTML,
                 * therefore encode it as HTML as well.
                 */
                var encodedResetUrl = HtmlEncoder.Default.Encode(resetUrl);
                var subject = localizer["PasswordReset_Subject"].Value;
                var heading = HtmlEncoder.Default.Encode(localizer["PasswordReset_Heading"].Value);
                var description = HtmlEncoder.Default.Encode(localizer["PasswordReset_Description"].Value);
                var button = HtmlEncoder.Default.Encode(localizer["PasswordReset_Button"].Value);
                var footer = HtmlEncoder.Default.Encode(localizer["PasswordReset_Footer"].Value);

                var htmlContent =
                    $$"""
                    <!doctype html>
                    <html lang="{{cultureInfo.TwoLetterISOLanguageName}}">
                    <head>
                        <meta charset="utf-8" />
                        <meta
                            name="viewport"
                            content="width=device-width, initial-scale=1"
                        />
                    </head>

                    <body
                        style="
                            margin: 0;
                            padding: 0;
                            background: #f8fafc;
                            font-family: Arial, Helvetica, sans-serif;
                            color: #0f172a;
                        "
                    >
                        <div
                            style="
                                width: 100%;
                                padding: 32px 16px;
                                box-sizing: border-box;
                            "
                        >
                            <div
                                style="
                                    max-width: 560px;
                                    margin: 0 auto;
                                    background: #ffffff;
                                    padding: 32px;
                                    border-radius: 16px;
                                    border: 1px solid #e2e8f0;
                                    box-sizing: border-box;
                                "
                            >
                                <div
                                    style="
                                        margin-bottom: 28px;
                                        font-size: 22px;
                                        font-weight: 700;
                                        color: #0f172a;
                                    "
                                >
                                    Averio
                                </div>

                                <h1
                                    style="
                                        margin: 0 0 16px;
                                        font-size: 28px;
                                        line-height: 1.25;
                                        color: #0f172a;
                                    "
                                >
                                    {{heading}}
                                </h1>

                                <p
                                    style="
                                        margin: 0;
                                        font-size: 16px;
                                        line-height: 1.6;
                                        color: #475569;
                                    "
                                >
                                    {{description}}
                                </p>

                                <div
                                    style="
                                        margin: 32px 0;
                                    "
                                >
                                    <a
                                        href="{{encodedResetUrl}}"
                                        style="
                                            display: inline-block;
                                            padding: 13px 22px;
                                            background: #10B981;
                                            color: #0f172a;
                                            text-decoration: none;
                                            font-size: 15px;
                                            font-weight: 700;
                                            border-radius: 10px;
                                        "
                                    >
                                        {{button}}
                                    </a>
                                </div>

                                <p
                                    style="
                                        margin: 0;
                                        font-size: 13px;
                                        line-height: 1.6;
                                        color: #64748b;
                                    "
                                >
                                    {{footer}}
                                </p>
                            </div>
                        </div>
                    </body>
                    </html>
                    """;

                await transactionalEmailSender
                    .SendAsync(
                        email,
                        subject,
                        htmlContent,
                        cancellationToken);

                return true;
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }
        catch (OperationCanceledException) when (cancellationToken .IsCancellationRequested)
        {
            /*
             * Cancellation is not an email-delivery
             * failure. Let it propagate to ASP.NET.
             */
            throw;
        }
        catch (Exception exception)
        {
            /*
             * Deliberately do NOT log the reset token
             * or the generated reset URL.
             */
            logger.LogError(exception, "Could not send password reset email to user {UserId}", userId);

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