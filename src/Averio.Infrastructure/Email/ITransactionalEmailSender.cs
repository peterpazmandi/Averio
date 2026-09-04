namespace Averio.Infrastructure.Email;

internal interface ITransactionalEmailSender
{
    Task SendAsync(
        string recipient,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken);
}