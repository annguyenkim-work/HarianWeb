using NewHarian.Application.Shipping;

namespace NewHarian.UnitTests.Application.Shipping;

[Trait("Category", "Unit")]
public class ShippingFeesTests
{
    [Fact]
    public void Default_province_fee_is_30000_vnd()
        => Assert.Equal(30_000m, ShippingFees.DefaultVnd);
}
