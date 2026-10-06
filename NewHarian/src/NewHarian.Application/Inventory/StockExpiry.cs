namespace NewHarian.Application.Inventory;

/// <summary>Single definition of "expired" for lots: HSD before today (UTC date).</summary>
public static class StockExpiry
{
    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(utcNow.Date);

    public static DateOnly TodayUtc() => Today(DateTime.UtcNow);

    public static bool IsExpired(DateOnly expiryDate, DateOnly today) => expiryDate < today;
}
