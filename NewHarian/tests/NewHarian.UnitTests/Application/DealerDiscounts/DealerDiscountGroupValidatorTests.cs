using NewHarian.Application.DealerDiscounts;

namespace NewHarian.UnitTests.Application.DealerDiscounts;

[Trait("Category", "Unit")]
public class DealerDiscountGroupValidatorTests
{
    private static DealerDiscountTierInput Tier(int min, int? max, decimal? a = 10, decimal? b = 12)
        => new() { MinQuantity = min, MaxQuantity = max, Percents = new() { [1] = a, [2] = b } };

    private static DealerDiscountGroupInput Valid() => new()
    {
        Name = "KIRARA",
        ProductIds = [1, 2],
        Tiers = [Tier(1, 19), Tier(20, 50), Tier(51, null)],
    };

    [Fact]
    public void Valid_group_passes()
    {
        Assert.Null(DealerDiscountGroupValidator.Validate(Valid()));
    }

    [Fact]
    public void Single_open_tier_passes()
    {
        var g = Valid();
        g.Tiers = [Tier(1, null)];
        Assert.Null(DealerDiscountGroupValidator.Validate(g));
    }

    public static TheoryData<string, Action<DealerDiscountGroupInput>> Invalid => new()
    {
        { "Tên nhóm phải từ 2-200 ký tự.", g => g.Name = "K" },
        { "Chọn ít nhất 1 sản phẩm.", g => g.ProductIds = [] },
        { "Sản phẩm bị chọn trùng.", g => g.ProductIds = [1, 1] },
        { "Cần ít nhất 1 mức số lượng.", g => g.Tiers = [] },
        { "Mức đầu tiên phải bắt đầu từ 1.", g => g.Tiers[0].MinQuantity = 2 },
        { "Mức 2: 'Từ' phải bằng 'Đến' của mức trước + 1.", g => g.Tiers[1].MinQuantity = 21 },
        { "Mức cuối phải để trống 'Đến' (trở lên).", g => g.Tiers[2].MaxQuantity = 100 },
        { "Mức 2: chỉ mức cuối được để trống 'Đến'.", g => g.Tiers[1].MaxQuantity = null },
        { "Mức 1: 'Đến' phải lớn hơn hoặc bằng 'Từ'.", g => g.Tiers[0].MaxQuantity = 0 },
        { "Mức 3: nhập đủ % cho mọi sản phẩm.", g => g.Tiers[2].Percents[2] = null },
        { "Mức 1: nhập đủ % cho mọi sản phẩm.", g => g.Tiers[0].Percents.Remove(1) },
        { "Mức 2: % phải từ 0 đến 100, tối đa 2 chữ số thập phân.", g => g.Tiers[1].Percents[1] = 100.01m },
        { "Mức 2: % phải từ 0 đến 100, tối đa 2 chữ số thập phân.", g => g.Tiers[1].Percents[1] = 12.345m },
        { "Mức 1: % phải từ 0 đến 100, tối đa 2 chữ số thập phân.", g => g.Tiers[0].Percents[2] = -1 },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Invalid_groups_return_first_error(string expected, Action<DealerDiscountGroupInput> mutate)
    {
        var g = Valid();
        mutate(g);
        Assert.Equal(expected, DealerDiscountGroupValidator.Validate(g));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("100", true)]
    [InlineData("22.5", true)]
    [InlineData("22.55", true)]
    [InlineData("22.555", false)]
    [InlineData("-0.01", false)]
    [InlineData("100.5", false)]
    public void IsPercent_accepts_0_to_100_with_two_decimals(string value, bool expected)
    {
        Assert.Equal(expected, DealerDiscountGroupValidator.IsPercent(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
