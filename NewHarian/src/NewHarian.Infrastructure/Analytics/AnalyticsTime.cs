namespace NewHarian.Infrastructure.Analytics;

public static class AnalyticsTime
{
    private const int MaxRangeDays = 366;

    private static readonly TimeZoneInfo LocalTz = ResolveLocalTz();

    public static DateOnly TodayLocal()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LocalTz));

    public static Application.Analytics.AnalyticsDateRange NormalizeRange(DateOnly? start, DateOnly? end)
        => NormalizeRange(start, end, TodayLocal());

    public static Application.Analytics.AnalyticsDateRange NormalizeRange(DateOnly? start, DateOnly? end, DateOnly today)
    {
        var defaultEnd = today;
        var defaultStart = today.AddDays(-29);
        var s = start ?? defaultStart;
        var e = end ?? defaultEnd;
        if (s > e) (s, e) = (e, s);
        var days = e.DayNumber - s.DayNumber + 1;
        if (days > MaxRangeDays)
            s = e.AddDays(-(MaxRangeDays - 1));
        return new Application.Analytics.AnalyticsDateRange(s, e);
    }

    private static TimeZoneInfo ResolveLocalTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException) { /* Windows */ }
        catch (InvalidTimeZoneException) { /* ignore */ }

        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }
}
