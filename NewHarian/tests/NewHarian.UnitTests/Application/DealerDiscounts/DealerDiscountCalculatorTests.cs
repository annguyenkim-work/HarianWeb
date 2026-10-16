using NewHarian.Application.DealerDiscounts;
using NewHarian.Domain.Enums;

namespace NewHarian.UnitTests.Application.DealerDiscounts;

[Trait("Category", "Unit")]
public class DealerDiscountCalculatorTests
{
    private const int Coral = 1;
    private const int Mau = 2;
    private const int Other = 3;
    private const int Unlisted = 99;

    // KIRARA (báo giá): 1-19 / 20-50 / Từ 51; Coral 20/25/30, Màu 18/22/27.
    private static DiscountGroupConfig Kirara() => new(10, "KIRARA", [Coral, Mau],
    [
        new DiscountTierConfig(101, 1, 19, new Dictionary<int, decimal> { [Coral] = 20, [Mau] = 18 }),
        new DiscountTierConfig(102, 20, 50, new Dictionary<int, decimal> { [Coral] = 25, [Mau] = 22 }),
        new DiscountTierConfig(103, 51, null, new Dictionary<int, decimal> { [Coral] = 30, [Mau] = 27 }),
    ]);

    private static DiscountGroupConfig OtherGroup() => new(20, "Khác", [Other],
    [
        new DiscountTierConfig(201, 1, null, new Dictionary<int, decimal> { [Other] = 10 }),
    ]);

    private static DealerDiscountPolicy Policy(Dictionary<(int, int), decimal>? overrides = null)
        => new([Kirara(), OtherGroup()], overrides ?? new Dictionary<(int, int), decimal>());

    [Theory]
    [InlineData(1, 20, "KIRARA: 1-19")]
    [InlineData(19, 20, "KIRARA: 1-19")]
    [InlineData(20, 25, "KIRARA: 20-50")]
    [InlineData(50, 25, "KIRARA: 20-50")]
    [InlineData(51, 30, "KIRARA: Từ 51")]
    [InlineData(500, 30, "KIRARA: Từ 51")]
    public void Tier_boundaries_follow_quantity(int qty, int expectedPercent, string expectedLabel)
    {
        var result = DealerDiscountCalculator.Calculate(Policy(), [new DiscountLineInput(Coral, qty, qty * 100_000m)]);

        var line = Assert.Single(result.Lines);
        Assert.Equal(expectedPercent, line.Percent);
        Assert.Equal(expectedLabel, line.TierLabel);
        Assert.Equal(DealerDiscountSource.Default, line.Source);
        Assert.False(line.Unconfigured);
    }

    [Fact]
    public void Quantity_is_summed_per_group_across_products()
    {
        var result = DealerDiscountCalculator.Calculate(Policy(),
        [
            new DiscountLineInput(Coral, 15, 1_500_000m),
            new DiscountLineInput(Mau, 10, 1_000_000m),
        ]);

        Assert.Equal(25, result.Lines[0].Percent);
        Assert.Equal(22, result.Lines[1].Percent);
        Assert.Equal(375_000m, result.Lines[0].Amount);
        Assert.Equal(220_000m, result.Lines[1].Amount);
        Assert.Equal(595_000m, result.TotalDiscount);
    }

    [Fact]
    public void Groups_are_counted_independently()
    {
        var result = DealerDiscountCalculator.Calculate(Policy(),
        [
            new DiscountLineInput(Coral, 5, 500_000m),
            new DiscountLineInput(Other, 30, 3_000_000m),
        ]);

        Assert.Equal(20, result.Lines[0].Percent);
        Assert.Equal(10, result.Lines[1].Percent);
        Assert.Equal("Khác: Từ 1", result.Lines[1].TierLabel);
    }

    [Fact]
    public void Same_product_on_two_lines_counts_both_quantities()
    {
        var result = DealerDiscountCalculator.Calculate(Policy(),
        [
            new DiscountLineInput(Coral, 10, 1_000_000m),
            new DiscountLineInput(Coral, 10, 1_000_000m),
        ]);

        Assert.All(result.Lines, l => Assert.Equal(25, l.Percent));
    }

    [Fact]
    public void Product_outside_any_group_gets_zero_and_is_flagged()
    {
        var result = DealerDiscountCalculator.Calculate(Policy(), [new DiscountLineInput(Unlisted, 100, 1_000_000m)]);

        var line = Assert.Single(result.Lines);
        Assert.Equal(0, line.Percent);
        Assert.Equal(0, line.Amount);
        Assert.True(line.Unconfigured);
        Assert.Equal(DealerDiscountSource.None, line.Source);
        Assert.Null(line.TierLabel);
    }

    [Fact]
    public void Dealer_override_replaces_default_only_for_its_cell()
    {
        var policy = Policy(new() { [(102, Coral)] = 28 });

        var result = DealerDiscountCalculator.Calculate(policy,
        [
            new DiscountLineInput(Coral, 15, 1_000_000m),
            new DiscountLineInput(Mau, 10, 1_000_000m),
        ]);

        Assert.Equal(28, result.Lines[0].Percent);
        Assert.Equal(DealerDiscountSource.DealerOverride, result.Lines[0].Source);
        Assert.Equal(22, result.Lines[1].Percent);
        Assert.Equal(DealerDiscountSource.Default, result.Lines[1].Source);
    }

    [Fact]
    public void Manual_percent_wins_and_keeps_policy_percent_for_audit()
    {
        var result = DealerDiscountCalculator.Calculate(Policy(),
            [new DiscountLineInput(Coral, 5, 1_000_000m, ManualPercent: 35)]);

        var line = Assert.Single(result.Lines);
        Assert.Equal(35, line.Percent);
        Assert.Equal(20, line.PolicyPercent);
        Assert.Equal(DealerDiscountSource.Manual, line.Source);
        Assert.Equal(350_000m, line.Amount);
    }

    [Fact]
    public void Manual_zero_percent_on_unconfigured_product_is_still_manual()
    {
        var result = DealerDiscountCalculator.Calculate(Policy(),
            [new DiscountLineInput(Unlisted, 1, 100_000m, ManualPercent: 5)]);

        Assert.Equal(DealerDiscountSource.Manual, result.Lines[0].Source);
        Assert.Equal(5_000m, result.Lines[0].Amount);
        Assert.True(result.Lines[0].Unconfigured);
    }

    [Fact]
    public void Empty_policy_gives_no_discount()
    {
        var result = DealerDiscountCalculator.Calculate(DealerDiscountPolicy.Empty, [new DiscountLineInput(Coral, 60, 1_000_000m)]);

        Assert.Equal(0, result.TotalDiscount);
        Assert.True(result.Lines[0].Unconfigured);
    }

    [Theory]
    [InlineData(333, "22.5", 75)]
    [InlineData(10, "25", 3)]
    [InlineData(1_000_001, "0.01", 100)]
    [InlineData(100_000, "0", 0)]
    public void AmountOf_rounds_half_away_from_zero_to_dong(int total, string percent, int expected)
    {
        Assert.Equal(expected, DealerDiscountCalculator.AmountOf(total, decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(1, 19, "1-19")]
    [InlineData(51, null, "Từ 51")]
    public void TierRange_formats_open_and_closed_tiers(int min, int? max, string expected)
    {
        Assert.Equal(expected, DealerDiscountCalculator.TierRange(min, max));
    }
}
