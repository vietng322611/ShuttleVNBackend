namespace ShuttleVNBackend.Application.Common;

public static class VietnamTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
                return zone;

        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam");
    }

    public static DateTime NowLocal(TimeProvider clock)
    {
        return TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime;
    }

    public static DateOnly TodayLocal(TimeProvider clock)
    {
        return DateOnly.FromDateTime(NowLocal(clock));
    }

    public static DateTime ToLocalDateTime(DateOnly date, TimeOnly time)
    {
        return date.ToDateTime(time, DateTimeKind.Unspecified);
    }

    public static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        return TimeZoneInfo.ConvertTimeToUtc(ToLocalDateTime(date, time), Zone);
    }
}