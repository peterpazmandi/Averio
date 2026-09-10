using System.Threading.Channels;
using Averio.Application.Identity;
using Averio.Application.Identity.ResendVerification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Averio.Infrastructure.Email;

/// <summary>
/// Buffers email verification resends in-memory and delivers them off the
/// request path, one at a time, via a background hosted service.
/// </summary>
internal sealed class EmailVerificationQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailVerificationQueue> logger)
    : BackgroundService, IEmailVerificationQueue
{
    private readonly Channel<EmailVerificationJob> _channel =
        Channel.CreateBounded<EmailVerificationJob>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public void Enqueue(EmailVerificationJob job)
    {
        if (!_channel.Writer.TryWrite(job))
        {
            logger.LogWarning(
                "Dropping queued verification email for user {UserId} because the queue is full",
                job.UserId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailVerificationSender>();

                await sender.TrySendAsync(
                    job.UserId,
                    job.Email,
                    job.VerificationToken,
                    job.Culture,
                    stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Failed to deliver queued verification email for user {UserId}",
                    job.UserId);
            }
        }
    }
}
