using Averio.Application.Receipts;
using Averio.Infrastructure.Receipts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Averio.Worker;

internal sealed class ReceiptWorker(IServiceScopeFactory scopes, ReceiptOptions options,
    IOptions<ReceiptStorageOptions> storage, ILogger<ReceiptWorker> logger,
    IOptions<GeminiReceiptOptions>? provider = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var storageConfigured = !string.IsNullOrWhiteSpace(storage.Value.Bucket);
        logger.LogInformation("Receipt worker starting: ProcessingEnabled={ProcessingEnabled}, StorageConfigured={StorageConfigured}, ProviderKeyConfigured={ProviderKeyConfigured}, ProviderModelConfigured={ProviderModelConfigured}, PollSeconds={PollSeconds}",
            options.Enabled, storageConfigured, !string.IsNullOrWhiteSpace(provider?.Value.ApiKey), !string.IsNullOrWhiteSpace(provider?.Value.Model), 10);
        if (!options.Enabled)
            logger.LogWarning("Receipt processing is disabled in this Worker process. Queued receipts will not be claimed. API and Worker configuration are independent; set Receipts__Enabled=true on the Worker and restart it");
        if (!options.Enabled && !storageConfigured)
        {
            logger.LogWarning("Receipt worker stopped: processing disabled and storage not configured; cleanup is also inactive");
            return;
        }
        try { await Task.WhenAll(CleanupLoopAsync(stoppingToken), ProcessingLoopAsync(stoppingToken)); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { logger.LogInformation("Receipt worker stopped"); }
    }
    private async Task CleanupLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var failures = await scope.ServiceProvider.GetRequiredService<ReceiptProcessor>().CleanupAsync(stoppingToken);
                if (failures > 0) logger.LogWarning("Receipt cleanup deferred {Count} failed deletions", failures);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Receipt cleanup poll failed: ExceptionType={ExceptionType}, HResult={HResult}; retrying next poll", ex.GetType().Name, ex.HResult); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
    private async Task ProcessingLoopAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
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
