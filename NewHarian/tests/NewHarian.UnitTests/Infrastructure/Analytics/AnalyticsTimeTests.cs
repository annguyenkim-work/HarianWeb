using NewHarian.Application.Analytics;
using NewHarian.Infrastructure.Analytics;

namespace NewHarian.UnitTests.Infrastructure.Analytics;

[Trait("Category", "Unit")]
public class AnalyticsTimeTests
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    [Fact]
    public void NormalizeRange_defaults_to_last_30_days()
    {
        Assert.Equal(new AnalyticsDateRange(new(2026, 9, 18), Today),
            AnalyticsTime.NormalizeRange(null, null, Today));
    }

    [Fact]
    public void NormalizeRange_swaps_and_caps_at_366_days()
    {
        Assert.Equal(new AnalyticsDateRange(new(2026, 3, 1), new(2026, 3, 15)),
            AnalyticsTime.NormalizeRange(new(2026, 3, 15), new(2026, 3, 1), Today));

        var wide = AnalyticsTime.NormalizeRange(new(2020, 1, 1), Today, Today);
        Assert.Equal(366, wide.End.DayNumber - wide.Start.DayNumber + 1);
    }
}
