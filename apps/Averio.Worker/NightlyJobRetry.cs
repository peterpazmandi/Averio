using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace Averio.Worker;

internal static class NightlyJobRetry
{
    private const int MaxAttempts = 3;

    internal static bool IsTransient(Exception error) =>
        error is TimeoutException or DbException { IsTransient: true };

    internal static async Task RunAsync(string job, Func<CancellationToken, Task> run, TimeProvider clock,
        ILogger logger, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await run(ct);
                logger.LogInformation("Nightly job completed: {Job}", job);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                // The first delay also allows a failed receipt cleanup reservation to expire.
                var delay = TimeSpan.FromMinutes(2 * attempt);
                logger.LogWarning("Nightly job {Job} will retry in {DelaySeconds}s: ExceptionType={ExceptionType}, HResult={HResult}",
                    job, delay.TotalSeconds, ex.GetType().Name, ex.HResult);
                await Task.Delay(delay, clock, ct);
            }
            catch (Exception ex)
            {
                logger.LogError("Nightly job failed: {Job}, ExceptionType={ExceptionType}, HResult={HResult}",
                    job, ex.GetType().Name, ex.HResult);
                return;
            }
        }
    }
}
