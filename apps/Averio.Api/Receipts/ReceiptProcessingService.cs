using Averio.Application.Receipts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Averio.Api.Receipts;

internal sealed class ReceiptProcessingService(
    IServiceScopeFactory scopes,
    ReceiptOptions options,
    ReceiptProcessingSignal signal,
    TimeProvider clock,
    ILogger<ReceiptProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Receipt extraction is disabled in the API");
            return;
        }

        logger.LogInformation("Receipt extraction started in the API");
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTimeOffset? nextAttempt;
            try
            {
                nextAttempt = await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogError("Receipt queue recovery failed: ExceptionType={ExceptionType}, HResult={HResult}",
                    ex.GetType().Name, ex.HResult);
                // Preserve the wake-up across a temporary database outage.
                await Task.Delay(TimeSpan.FromSeconds(30), clock, stoppingToken);
                continue;
            }

            await WaitForWorkAsync(nextAttempt, stoppingToken);
        }
    }

    private async Task<DateTimeOffset?> DrainAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await using var scope = scopes.CreateAsyncScope();
            var services = scope.ServiceProvider;
            if (await services.GetRequiredService<ReceiptProcessor>().ProcessNextAsync(ct)) continue;

            return await services.GetRequiredService<IReceiptRepository>().NextProcessingAtAsync(clock.GetUtcNow(), ct);
        }
    }

    private async Task WaitForWorkAsync(DateTimeOffset? nextAttempt, CancellationToken ct)
    {
        if (nextAttempt is not { } next)
        {
            await signal.WaitAsync(ct);
            return;
        }

        var delay = next - clock.GetUtcNow();
        // A due row can be temporarily locked by another process during deployment.
        using var timer = new CancellationTokenSource(delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1), clock);
        using var wake = CancellationTokenSource.CreateLinkedTokenSource(ct, timer.Token);
        try { await signal.WaitAsync(wake.Token); }
        catch (OperationCanceledException) when (timer.IsCancellationRequested && !ct.IsCancellationRequested) { }
    }
}
