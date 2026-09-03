using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Averio.Infrastructure.Email;

internal sealed class BrevoTransactionalEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options) : ITransactionalEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(
        string recipient,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BrevoApiKey))
            throw new InvalidOperationException("Brevo API key is not configured.");

        if (string.IsNullOrWhiteSpace(_options.SenderEmail))
            throw new InvalidOperationException("Email sender address is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");

        request.Headers.Add("api-key", _options.BrevoApiKey);

        request.Content = JsonContent.Create(new
        {
            sender = new
            {
                name = _options.SenderName,
                email = _options.SenderEmail
            },
            to = new[]
            {
                new
                {
                    email = recipient,
                    contactPixelTrackingConsent = false
                }
            },
            subject,
            htmlContent,
            tags = new[] { "email-verification" }
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Brevo returned HTTP {(int)response.StatusCode}.");
        }
    }
}