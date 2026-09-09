using System.Threading.Channels;
using Averio.Application.Identity;
using Averio.Application.Identity.RequestPasswordReset;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Averio.Infrastructure.Email;

/// <summary>
/// Buffers password reset emails in-memory and delivers them off the
/// request path, one at a time, via a background hosted service.
/// </summary>
internal sealed class PasswordResetEmailQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<PasswordResetEmailQueue> logger)
    : BackgroundService, IPasswordResetEmailQueue
{
    private readonly Channel<PasswordResetEmailJob> _channel =
        Channel.CreateUnbounded<PasswordResetEmailJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(PasswordResetEmailJob job)
    {
        _channel.Writer.TryWrite(job);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<IPasswordResetSender>();

                await sender.TrySendAsync(
                    job.UserId,
                    job.Email,
                    job.ResetToken,
                    job.Culture,
                    stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Failed to deliver queued password reset email for user {UserId}",
                    job.UserId);
            }
        }
    }
}
