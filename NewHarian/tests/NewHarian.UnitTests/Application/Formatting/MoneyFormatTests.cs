using NewHarian.Application.Formatting;

namespace NewHarian.UnitTests.Application.Formatting;

[Trait("Category", "Unit")]
public class MoneyFormatTests
{
    [Theory]
    [InlineData(0, "0 VNĐ")]
    [InlineData(1500, "1.500 VNĐ")]
    [InlineData(1_500_000, "1.500.000 VNĐ")]
    [InlineData(12_000_000, "12.000.000 VNĐ")]
    public void Vnd_formats_with_vi_thousands_and_suffix(decimal amount, string expected)
        => Assert.Equal(expected, MoneyFormat.Vnd(amount));

    [Fact]
    public void Vnd_nullable_empty_when_null()
        => Assert.Equal("", MoneyFormat.Vnd(null));

    [Fact]
    public void VndSigned_keeps_minus_before_amount()
        => Assert.Equal("-595.000 VNĐ", MoneyFormat.VndSigned(-595_000m));

    [Theory]
    [InlineData("1500000", 1_500_000)]
    [InlineData("1.500.000", 1_500_000)]
    [InlineData("1.500.000 VNĐ", 1_500_000)]
    [InlineData("1500000đ", 1_500_000)]
    [InlineData(" 12.000.000 VND ", 12_000_000)]
    [InlineData("0", 0)]
    public void TryParse_accepts_plain_grouped_and_suffixed(string raw, decimal expected)
    {
        Assert.True(MoneyFormat.TryParse(raw, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1.5")]
    [InlineData("1,500,000")]
    [InlineData("abc")]
    [InlineData("1.500.000.1")]
    public void TryParse_rejects_invalid(string? raw)
        => Assert.False(MoneyFormat.TryParse(raw, out _));
}
