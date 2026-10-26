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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = scopes.CreateScope())
                    await scope.ServiceProvider.GetRequiredService<ReceiptProcessor>().CleanupAsync(stoppingToken);
                for (var i = 0; i < 10 && options.Enabled; i++)
                {
                    using var scope = scopes.CreateScope();
                    if (!await scope.ServiceProvider.GetRequiredService<ReceiptProcessor>().ProcessNextAsync(stoppingToken)) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Receipt batch failed; retrying next poll"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
