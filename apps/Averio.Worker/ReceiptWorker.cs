using Averio.Application.Receipts;
using Averio.Infrastructure.Receipts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Averio.Worker;

internal sealed class ReceiptWorker(IServiceScopeFactory scopes, ReceiptOptions options,
    IOptions<ReceiptStorageOptions> storage, ILogger<ReceiptWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled && string.IsNullOrWhiteSpace(storage.Value.Bucket)) return;
        // Separate scopes and loops: a slow/failed delete must not block extraction,
        // and a backlog of provider requests must not delay retention cleanup.
        await Task.WhenAll(CleanupLoopAsync(stoppingToken), ProcessingLoopAsync(stoppingToken));
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
            catch (Exception) { logger.LogError("Receipt cleanup failed; retrying next poll"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
    private async Task ProcessingLoopAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var i = 0; i < 10; i++)
                {
                    using var scope = scopes.CreateScope();
                    if (!await scope.ServiceProvider.GetRequiredService<ReceiptProcessor>().ProcessNextAsync(stoppingToken)) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Receipt extraction batch failed; retrying next poll"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
