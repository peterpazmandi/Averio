using Averio.Worker;

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

    private static void Check(DateTimeOffset now, DateTimeOffset expected)
    {
        var actual = NightlySchedule.NextRun(now);
        if (!actual.EqualsExact(expected))
            throw new Exception($"Expected next run at {expected:o} after {now:o}, got {actual:o}.");
    }
}
