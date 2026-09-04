using Microsoft.Extensions.Logging;

namespace Averio.Infrastructure.Email;

internal sealed class DevelopmentTransactionalEmailSender(
    ILogger<DevelopmentTransactionalEmailSender> logger) : ITransactionalEmailSender
{
    public Task SendAsync(
        string recipient,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            """
            DEVELOPMENT EMAIL

            To:
            {Recipient}

            Subject:
            {Subject}

            Body:
            {HtmlContent}
            """,
            recipient,
            subject,
            htmlContent);

        return Task.CompletedTask;
    }
}