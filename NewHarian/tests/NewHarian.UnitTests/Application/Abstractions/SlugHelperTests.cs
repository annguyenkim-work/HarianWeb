using NewHarian.Application.Abstractions;

namespace NewHarian.UnitTests.Application.Abstractions;

[Trait("Category", "Unit")]
public class SlugHelperTests
{
    [Theory]
    [InlineData("Đồng hồ Đeo tay", "dong-ho-deo-tay")]
    [InlineData("đường", "duong")]
    [InlineData("  Tiếng Việt có dấu  ", "tieng-viet-co-dau")]
    [InlineData("ÁO DÀI", "ao-dai")]
    [InlineData("Phở Bò", "pho-bo")]
    [InlineData("Ơn ưu", "on-uu")]
    [InlineData("Sản phẩm #1 (mới)!", "san-pham-1-moi")]
    [InlineData("a -- b", "a-b")]
    [InlineData("-Leading and trailing-", "leading-and-trailing")]
    [InlineData("Hello_World", "hello-world")]
    [InlineData("Café", "cafe")]
    [InlineData("SKU 2026", "sku-2026")]
    public void FromVietnamese_builds_ascii_slug(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.FromVietnamese(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void FromVietnamese_returns_empty_when_nothing_slug_worthy(string? input)
    {
        Assert.Equal(string.Empty, SlugHelper.FromVietnamese(input));
    }

    [Fact]
    public void FromVietnamese_treats_decomposed_and_precomposed_input_the_same()
    {
        var precomposed = "Việt";
        var decomposed = precomposed.Normalize(System.Text.NormalizationForm.FormD);

        Assert.Equal(SlugHelper.FromVietnamese(precomposed), SlugHelper.FromVietnamese(decomposed));
    }
}
