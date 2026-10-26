using NewHarian.Application.Cart;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Domain.Enums;

namespace NewHarian.UnitTests.Application.Cart;

[Trait("Category", "Unit")]
public class CartPricingTests
{
    private const int Coral = 1;
    private const int Mau = 2;
    private const int Loose = 3;

    private static CartItemDto Item(int variantId, int qty, decimal price = 100_000m)
        => new(variantId, variantId, $"P{variantId}", "1kg", $"SKU-{variantId}", price, qty, null);

    private static DealerDiscountPolicy Kirara(Dictionary<(int, int), decimal>? overrides = null) => new(
    [
        new DiscountGroupConfig(10, "KIRARA", [Coral, Mau],
        [
            new DiscountTierConfig(101, 1, 19, new Dictionary<int, decimal> { [Coral] = 20, [Mau] = 18 }),
            new DiscountTierConfig(102, 20, 50, new Dictionary<int, decimal> { [Coral] = 25, [Mau] = 22.5m }),
            new DiscountTierConfig(103, 51, null, new Dictionary<int, decimal> { [Coral] = 30, [Mau] = 27 }),
        ]),
    ], overrides ?? new Dictionary<(int, int), decimal>());

    [Fact]
    public void Guest_cart_keeps_list_prices()
    {
        var cart = new CartDto([Item(Coral, 15), Item(Mau, 10)]);

        var priced = CartPricing.Apply(cart, null);

        Assert.False(priced.IsDealer);
        Assert.Equal(0m, priced.DiscountAmount);
        Assert.Equal(2_500_000m, priced.NetSubTotal);
        Assert.All(priced.Lines, l =>
        {
            Assert.Equal(0m, l.DiscountPercent);
            Assert.Equal(DealerDiscountSource.None, l.DiscountSource);
            Assert.Equal(l.Item.LineTotal, l.NetTotal);
        });
    }

    [Fact]
    public void Dealer_cart_sums_group_quantity_and_discounts_each_line()
    {
        var cart = new CartDto([Item(Coral, 15), Item(Mau, 10)]);

        var priced = CartPricing.Apply(cart, Kirara());

        Assert.True(priced.IsDealer);
        Assert.Equal((25m, 375_000m, "KIRARA: 20-50"),
            (priced.Lines[0].DiscountPercent, priced.Lines[0].DiscountAmount, priced.Lines[0].DiscountTierLabel));
        Assert.Equal((22.5m, 225_000m), (priced.Lines[1].DiscountPercent, priced.Lines[1].DiscountAmount));
        Assert.Equal(600_000m, priced.DiscountAmount);
        Assert.Equal(2_500_000m, priced.SubTotal);
        Assert.Equal(1_900_000m, priced.NetSubTotal);
        Assert.Equal(1_125_000m, priced.Lines[0].NetTotal);
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(19, 20)]
    [InlineData(20, 25)]
    [InlineData(51, 30)]
    public void Dealer_tier_follows_cart_quantity(int qty, int expected)
    {
        var priced = CartPricing.Apply(new CartDto([Item(Coral, qty)]), Kirara());

        Assert.Equal(expected, priced.Lines[0].DiscountPercent);
    }

    [Fact]
    public void Dealer_override_wins_over_default()
    {
        var priced = CartPricing.Apply(new CartDto([Item(Coral, 15), Item(Mau, 10)]),
            Kirara(new Dictionary<(int, int), decimal> { [(102, Coral)] = 28 }));

        Assert.Equal((28m, DealerDiscountSource.DealerOverride), (priced.Lines[0].DiscountPercent, priced.Lines[0].DiscountSource));
        Assert.Equal(DealerDiscountSource.Default, priced.Lines[1].DiscountSource);
    }

    [Fact]
    public void Product_outside_any_group_is_flagged_unconfigured_at_zero()
    {
        var priced = CartPricing.Apply(new CartDto([Item(Coral, 5), Item(Loose, 3, 50_000m)]), Kirara());

        var loose = priced.Lines[1];
        Assert.True(loose.Unconfigured);
        Assert.Equal(0m, loose.DiscountAmount);
        Assert.Equal(100_000m, priced.DiscountAmount);
    }

    [Fact]
    public void Empty_dealer_cart_has_no_discount()
    {
        var priced = CartPricing.Apply(new CartDto([]), Kirara());

        Assert.True(priced.IsDealer);
        Assert.Empty(priced.Lines);
        Assert.Equal(0m, priced.NetSubTotal);
    }
}
