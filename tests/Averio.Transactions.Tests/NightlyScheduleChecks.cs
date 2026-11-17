using Averio.Worker;
using Microsoft.Extensions.Logging.Abstractions;

internal static class NightlyScheduleChecks
{
    public static void Run()
    {
        Check(new(2026, 1, 10, 0, 0, 0, TimeSpan.Zero), new(2026, 1, 10, 3, 0, 0, TimeSpan.FromHours(1)));
        Check(new(2026, 7, 10, 2, 0, 0, TimeSpan.Zero), new(2026, 7, 11, 3, 0, 0, TimeSpan.FromHours(2)));
        Check(new(2026, 3, 28, 2, 0, 0, TimeSpan.Zero), new(2026, 3, 29, 3, 0, 0, TimeSpan.FromHours(2)));
        Check(new(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), new(2026, 10, 25, 3, 0, 0, TimeSpan.FromHours(1)));
        Check(new(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), new(2026, 10, 25, 3, 0, 0, TimeSpan.FromHours(1)));
    }

    public static async Task RetryAsync()
    {
        var clock = new FastClock();
        var attempts = 0;

        await NightlyJobRetry.RunAsync("transient recovery", _ =>
        {
            if (++attempts < 3) throw new TimeoutException();
            return Task.CompletedTask;
        }, clock, NullLogger.Instance, default);
        if (attempts != 3 || !clock.Delays.SequenceEqual(new[] { TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4) }))
            throw new Exception("A nightly job must retry transient failures within the same nightly window.");

        attempts = 0;
        await NightlyJobRetry.RunAsync("permanent failure", _ =>
        {
            attempts++;
            throw new InvalidOperationException();
        }, clock, NullLogger.Instance, default);
        if (attempts != 1 || clock.Delays.Count != 2)
            throw new Exception("Permanent errors must not trigger nightly retries.");

        attempts = 0;
        await NightlyJobRetry.RunAsync("retry budget exhausted", _ =>
        {
            attempts++;
            throw new TimeoutException();
        }, clock, NullLogger.Instance, default);
        if (attempts != 3 || clock.Delays.Count != 4)
            throw new Exception("A persistent transient error must stop after three attempts.");
    }

    private sealed class FastClock : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            return System.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    private static void Check(DateTimeOffset now, DateTimeOffset expected)
    {
        var actual = NightlySchedule.NextRun(now);
        if (!actual.EqualsExact(expected))
            throw new Exception($"Expected next run at {expected:o} after {now:o}, got {actual:o}.");
    }
}
