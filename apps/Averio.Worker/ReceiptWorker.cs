using Averio.Application.Receipts;
using Averio.Infrastructure.Receipts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Averio.Worker;

internal sealed class ReceiptWorker(IServiceScopeFactory scopes, ReceiptOptions options,
    ILogger<ReceiptWorker> logger,
    IOptions<GeminiReceiptOptions>? provider = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Receipt worker starting: ProcessingEnabled={ProcessingEnabled}, ProviderKeyConfigured={ProviderKeyConfigured}, ProviderModelConfigured={ProviderModelConfigured}, PollSeconds={PollSeconds}",
            options.Enabled, !string.IsNullOrWhiteSpace(provider?.Value.ApiKey), !string.IsNullOrWhiteSpace(provider?.Value.Model), 10);
        if (!options.Enabled)
        {
            logger.LogWarning("Receipt processing is disabled in this Worker process. Queued receipts will not be claimed. API and Worker configuration are independent; set Receipts__Enabled=true on the Worker and restart it");
            return;
        }

        try { await ProcessingLoopAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { logger.LogInformation("Receipt worker stopped"); }
    }

    private async Task ProcessingLoopAsync(CancellationToken stoppingToken)
    {
        var heartbeat = System.Diagnostics.Stopwatch.StartNew();
        var processed = 0;
        var first = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;
            try
            {
                for (var i = 0; i < 10; i++)
                {
                    using var scope = scopes.CreateScope();
                    if (!await scope.ServiceProvider.GetRequiredService<ReceiptProcessor>().ProcessNextAsync(stoppingToken)) break;
                    claimed++; processed++;
                }
                logger.LogDebug("Receipt queue poll completed: Claimed={Claimed}", claimed);
                if (first || heartbeat.Elapsed >= TimeSpan.FromMinutes(1))
                {
                    logger.LogInformation("Receipt worker heartbeat: ClaimedSinceHeartbeat={ClaimedSinceHeartbeat}, LastPollClaimed={LastPollClaimed}. Zero means no eligible rows were found in this Worker's configured database", processed, claimed);
                    processed = 0; first = false; heartbeat.Restart();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Receipt extraction poll failed: Claimed={Claimed}, ExceptionType={ExceptionType}, HResult={HResult}; retrying next poll", claimed, ex.GetType().Name, ex.HResult); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
