namespace NewHarian.Application.Dashboard;

/// <summary>Pure helpers for commerce KPI cards (AOV, period-over-period %).</summary>
public static class DashboardKpiMath
{
    public static decimal AverageOrderValue(decimal revenue, int revenueOrderCount)
        => revenueOrderCount <= 0 ? 0m : decimal.Round(revenue / revenueOrderCount, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Percent change of <paramref name="current"/> vs <paramref name="previous"/>.
    /// Null when previous is zero (no meaningful %).
    /// </summary>
    public static decimal? DeltaPercent(decimal current, decimal previous)
    {
        if (previous == 0m) return null;
        return decimal.Round((current - previous) / previous * 100m, 1, MidpointRounding.AwayFromZero);
    }

    public static decimal? DeltaPercent(int current, int previous)
        => DeltaPercent((decimal)current, previous);

    /// <summary>Previous period of the same length ending the day before <paramref name="range"/>.Start.</summary>
    public static DashboardDateRange PreviousPeriod(DashboardDateRange range)
    {
        var days = range.End.DayNumber - range.Start.DayNumber + 1;
        var prevEnd = range.Start.AddDays(-1);
        var prevStart = prevEnd.AddDays(-(days - 1));
        return new DashboardDateRange(prevStart, prevEnd);
    }
}
