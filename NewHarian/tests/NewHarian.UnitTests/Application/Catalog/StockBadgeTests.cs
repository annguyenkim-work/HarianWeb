using NewHarian.Application.Catalog;
using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;

namespace NewHarian.UnitTests.Application.Catalog;

[Trait("Category", "Unit")]
public class StockBadgeTests
{
    [Theory]
    [InlineData(24, null, StockBadgeLevel.Ok)]
    [InlineData(6, 5, StockBadgeLevel.Ok)]
    [InlineData(5, 5, StockBadgeLevel.Low)]
    [InlineData(1, null, StockBadgeLevel.Low)]
    [InlineData(0, 5, StockBadgeLevel.Out)]
    [InlineData(-1, null, StockBadgeLevel.Out)]
    public void Level_uses_variant_threshold_or_five(int qty, int? threshold, StockBadgeLevel expected)
    {
        Assert.Equal(expected, StockBadge.Level(qty, threshold));
    }

    [Theory]
    [InlineData(StockBadgeLevel.Ok, "badge-ok")]
    [InlineData(StockBadgeLevel.Low, "badge-warn")]
    [InlineData(StockBadgeLevel.Out, "badge-danger")]
    public void Css_class_matches_the_level(StockBadgeLevel level, string css)
    {
        Assert.Equal(css, StockBadge.CssClass(level));
    }

    [Theory]
    [InlineData(OrderStatus.Delivered, "badge-ok")]
    [InlineData(OrderStatus.PendingPayment, "badge-warn")]
    [InlineData(OrderStatus.AwaitingConfirmation, "badge-warn")]
    [InlineData(OrderStatus.Cancelled, "badge-danger")]
    [InlineData(OrderStatus.Refunded, "badge-danger")]
    [InlineData(OrderStatus.Processing, "badge-paper")]
    [InlineData(OrderStatus.Shipped, "badge-paper")]
    public void Order_status_badge_class(OrderStatus status, string css)
    {
        Assert.Equal(css, OrderStatusTone.BadgeClass(status));
        Assert.StartsWith("Orders.", OrderStatusTone.LabelKey(status));
    }
}
