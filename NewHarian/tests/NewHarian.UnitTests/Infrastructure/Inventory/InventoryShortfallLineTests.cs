using NewHarian.Application.Inventory;
using NewHarian.Infrastructure.Inventory;

namespace NewHarian.UnitTests.Infrastructure.Inventory;

[Trait("Category", "Unit")]
public class InventoryShortfallLineTests
{
    [Fact]
    public void Plain_shortfall_lists_missing_over_requested()
    {
        var plan = new FefoPlan(5, [], ExpiredOnHand: 0);

        Assert.Equal("SKU-1: thiếu 5/5", InventoryService.FormatShortfallLine("SKU-1", 5, plan));
    }

    [Fact]
    public void Mentions_expired_lots_when_stock_only_left_in_expired_lots()
    {
        var plan = new FefoPlan(3, [], ExpiredOnHand: 7);

        Assert.Equal(
            "SKU-1: thiếu 3/3 (còn 7 trong lô hết hạn, không xuất)",
            InventoryService.FormatShortfallLine("SKU-1", 3, plan));
    }
}
