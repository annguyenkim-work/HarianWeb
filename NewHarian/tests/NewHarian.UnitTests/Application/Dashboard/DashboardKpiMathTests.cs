using NewHarian.Application.Dashboard;

namespace NewHarian.UnitTests.Application.Dashboard;

[Trait("Category", "Unit")]
public class DashboardKpiMathTests
{
    [Theory]
    [InlineData(1_500_000, 3, 500_000)]
    [InlineData(0, 0, 0)]
    [InlineData(100, 0, 0)]
    [InlineData(1_000_001, 2, 500_001)]
    public void AverageOrderValue_rounds_away_from_zero(decimal revenue, int count, decimal expected)
        => Assert.Equal(expected, DashboardKpiMath.AverageOrderValue(revenue, count));

    [Theory]
    [InlineData(120, 100, 20.0)]
    [InlineData(80, 100, -20.0)]
    [InlineData(100, 100, 0.0)]
    public void DeltaPercent_decimal_computes_one_decimal_place(decimal current, decimal previous, double expected)
        => Assert.Equal((decimal)expected, DashboardKpiMath.DeltaPercent(current, previous));

    [Fact]
    public void DeltaPercent_null_when_previous_is_zero()
    {
        Assert.Null(DashboardKpiMath.DeltaPercent(50m, 0m));
        Assert.Null(DashboardKpiMath.DeltaPercent(10, 0));
    }

    [Fact]
    public void PreviousPeriod_same_length_ending_day_before_start()
    {
        var range = new DashboardDateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10));
        Assert.Equal(new DashboardDateRange(new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 30)),
            DashboardKpiMath.PreviousPeriod(range));
    }

    [Fact]
    public void PreviousPeriod_single_day()
    {
        var day = new DateOnly(2026, 5, 5);
        Assert.Equal(new DashboardDateRange(new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 4)),
            DashboardKpiMath.PreviousPeriod(new DashboardDateRange(day, day)));
    }
}
