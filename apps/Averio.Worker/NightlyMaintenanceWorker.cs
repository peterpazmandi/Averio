using Averio.Application.Receipts;
using Averio.Infrastructure.Receipts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Averio.Worker;

internal sealed class NightlyMaintenanceWorker(
    RecurringBatch recurring,
    RecurringAllowanceBatch allowance,
    IServiceScopeFactory scopes,
    ReceiptOptions receipts,
    IOptions<ReceiptStorageOptions> storage,
    TimeProvider clock,
    ILogger<NightlyMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            var runAt = NightlySchedule.NextRun(now);
            logger.LogInformation("Next nightly maintenance run: {RunAt} Europe/Budapest", runAt);
            await Task.Delay(runAt - now, clock, stoppingToken);

            await RunJobAsync("Recurring transactions", recurring.RunAsync, stoppingToken);
            await RunJobAsync("Recurring allowances", allowance.RunAsync, stoppingToken);
            if (receipts.Enabled || !string.IsNullOrWhiteSpace(storage.Value.Bucket))
                await RunJobAsync("Receipt cleanup", CleanupReceiptsAsync, stoppingToken);
        }
    }

    private async Task CleanupReceiptsAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var failures = await scope.ServiceProvider.GetRequiredService<ReceiptProcessor>().CleanupAsync(ct);
        if (failures > 0)
            logger.LogWarning("Receipt cleanup deferred {Count} failed deletions to a later run", failures);
    }

    private async Task RunJobAsync(string job, Func<CancellationToken, Task> run, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await run(ct);
            logger.LogInformation("Nightly job completed: {Job}", job);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Receipt cleanup errors can contain provider details. Log only the type.
            logger.LogError("Nightly job failed: {Job}, ExceptionType={ExceptionType}, HResult={HResult}",
                job, ex.GetType().Name, ex.HResult);
        }
    }
}
