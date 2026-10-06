using NewHarian.Application.Dashboard;
using NewHarian.Infrastructure.Dashboard;

namespace NewHarian.UnitTests.Infrastructure.Dashboard;

[Trait("Category", "Unit")]
public class AdminDashboardServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Fact]
    public void NormalizeRange_defaults_to_last_30_days_ending_today()
    {
        Assert.Equal(new DashboardDateRange(D(2026, 9, 7), Today), AdminDashboardService.NormalizeRange(null, null, Today));
    }

    [Fact]
    public void NormalizeRange_start_only_ends_today()
    {
        Assert.Equal(new DashboardDateRange(D(2026, 10, 1), Today), AdminDashboardService.NormalizeRange(D(2026, 10, 1), null, Today));
    }

    [Fact]
    public void NormalizeRange_end_only_uses_default_start_and_swaps_when_reversed()
    {
        var range = AdminDashboardService.NormalizeRange(null, D(2026, 1, 31), Today);

        Assert.Equal(new DashboardDateRange(D(2026, 1, 31), D(2026, 9, 7)), range);
    }

    [Fact]
    public void NormalizeRange_swaps_reversed_bounds()
    {
        Assert.Equal(new DashboardDateRange(D(2026, 3, 1), D(2026, 3, 15)),
            AdminDashboardService.NormalizeRange(D(2026, 3, 15), D(2026, 3, 1), Today));
    }

    [Fact]
    public void NormalizeRange_keeps_single_day()
    {
        Assert.Equal(new DashboardDateRange(D(2026, 5, 5), D(2026, 5, 5)),
            AdminDashboardService.NormalizeRange(D(2026, 5, 5), D(2026, 5, 5), Today));
    }

    [Fact]
    public void NormalizeRange_keeps_exactly_366_days()
    {
        var start = Today.AddDays(-365);

        Assert.Equal(new DashboardDateRange(start, Today), AdminDashboardService.NormalizeRange(start, Today, Today));
    }

    [Fact]
    public void NormalizeRange_clamps_longer_ranges_to_366_days_ending_at_end()
    {
        var range = AdminDashboardService.NormalizeRange(D(2024, 1, 1), Today, Today);

        Assert.Equal(new DashboardDateRange(Today.AddDays(-365), Today), range);
        Assert.Equal(366, range.End.DayNumber - range.Start.DayNumber + 1);
    }

    [Fact]
    public void NormalizeRange_without_today_uses_clock_and_30_day_default()
    {
        var range = AdminDashboardService.NormalizeRange(null, null);

        Assert.Equal(29, range.End.DayNumber - range.Start.DayNumber);
    }
}
