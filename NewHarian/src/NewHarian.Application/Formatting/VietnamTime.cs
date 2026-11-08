namespace NewHarian.Application.Formatting;

/// <summary>Vietnam civil time (UTC+7, no daylight saving) for admin date entry and display.</summary>
public static class VietnamTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone(
        "Asia/Ho_Chi_Minh", TimeSpan.FromHours(7), "Vietnam", "Vietnam");

    public static DateTime ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone);
    }

    public static DateTime ToLocal(DateTime utc)
    {
        var asUtc = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, Zone);
    }

    public static string Format(DateTime utc) => ToLocal(utc).ToString("dd/MM/yyyy HH:mm");

    public static string ForInput(DateTime utc) => ToLocal(utc).ToString("yyyy-MM-ddTHH:mm");
}
