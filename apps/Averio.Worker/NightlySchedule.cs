namespace Averio.Worker;

internal static class NightlySchedule
{
    private static readonly TimeZoneInfo Budapest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
    private static readonly TimeOnly StartTime = new(3, 0);

    internal static DateTimeOffset NextRun(DateTimeOffset now)
    {
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Budapest).DateTime);
        var localTime = date.ToDateTime(StartTime);
        var runAt = new DateTimeOffset(localTime, Budapest.GetUtcOffset(localTime));
        if (runAt > now) return runAt;

        // Use the next local date to stay at 03:00 across clock changes.
        localTime = date.AddDays(1).ToDateTime(StartTime);
        return new DateTimeOffset(localTime, Budapest.GetUtcOffset(localTime));
    }
}
