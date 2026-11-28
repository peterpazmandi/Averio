using Averio.Application.Receipts;
using Averio.Application.Notifications;
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

            await NightlyJobRetry.RunAsync("Recurring transactions", recurring.RunAsync, clock, logger, stoppingToken);
            await NightlyJobRetry.RunAsync("Recurring allowances", allowance.RunAsync, clock, logger, stoppingToken);
            await NightlyJobRetry.RunAsync("Notification reminders", ReconcileNotificationsAsync, clock, logger, stoppingToken);
            if (receipts.Enabled || !string.IsNullOrWhiteSpace(storage.Value.Bucket))
                await NightlyJobRetry.RunAsync("Receipt cleanup", CleanupReceiptsAsync, clock, logger, stoppingToken);
        }
    }

    private async Task CleanupReceiptsAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var failures = await scope.ServiceProvider.GetRequiredService<ReceiptProcessor>().CleanupAsync(ct);
        if (failures > 0)
            logger.LogWarning("Receipt cleanup deferred {Count} failed deletions to a later run", failures);
    }

    private async Task ReconcileNotificationsAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<INotificationInbox>().ReconcileAllAsync(ct);
    }
}
