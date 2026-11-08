namespace NewHarian.Application.Catalog;

public enum StockBadgeLevel
{
    Ok,
    Low,
    Out
}

/// <summary>Guest stock badge from the variant quantity cache. Low uses the variant threshold, otherwise 5.</summary>
public static class StockBadge
{
    public const int DefaultLowThreshold = 5;

    public static StockBadgeLevel Level(int available, int? lowThreshold)
    {
        var threshold = lowThreshold is > 0 ? lowThreshold.Value : DefaultLowThreshold;
        if (available <= 0) return StockBadgeLevel.Out;
        if (available <= threshold) return StockBadgeLevel.Low;
        return StockBadgeLevel.Ok;
    }

    public static string CssClass(StockBadgeLevel level) => level switch
    {
        StockBadgeLevel.Out => "badge-danger",
        StockBadgeLevel.Low => "badge-warn",
        _ => "badge-ok"
    };
}

public readonly record struct StockBadgeModel(int Available, int? LowThreshold);
