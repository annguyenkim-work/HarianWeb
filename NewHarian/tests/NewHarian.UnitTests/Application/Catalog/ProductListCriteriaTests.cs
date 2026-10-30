using NewHarian.Application.Catalog;

namespace NewHarian.UnitTests.Application.Catalog;

[Trait("Category", "Unit")]
public class ProductListCriteriaTests
{
    [Theory]
    [InlineData(null, ProductListSort.Recommended)]
    [InlineData("recommended", ProductListSort.Recommended)]
    [InlineData("NAME", ProductListSort.Name)]
    [InlineData("price-asc", ProductListSort.PriceAsc)]
    [InlineData("price-desc", ProductListSort.PriceDesc)]
    [InlineData("drop table", ProductListSort.Recommended)]
    public void ParseSort_only_accepts_known_keys(string? sort, ProductListSort expected)
    {
        Assert.Equal(expected, ProductListCriteria.ParseSort(sort));
    }

    [Fact]
    public void Normalize_swaps_reversed_prices_and_drops_invalid_values()
    {
        var criteria = ProductListCriteria.Normalize(0, "nope", "  hoa-chat ", "  Industrial_1 ", 50.4m, 10.2m);

        Assert.Equal(1, criteria.Page);
        Assert.Equal(ProductListSort.Recommended, criteria.Sort);
        Assert.Equal("hoa-chat", criteria.CategorySlug);
        Assert.Equal("industrial_1", criteria.TagSlug);
        Assert.Equal(10m, criteria.MinPrice);
        Assert.Equal(50m, criteria.MaxPrice);
        Assert.Equal(2, criteria.ActiveFilterCount);
    }

    [Fact]
    public void Normalize_ignores_negative_and_missing_prices()
    {
        var negative = ProductListCriteria.Normalize(1, null, null, null, -1m, -2m);
        var missing = ProductListCriteria.Normalize(1, null, null, null, null, null);

        Assert.Null(negative.MinPrice);
        Assert.Null(negative.MaxPrice);
        Assert.Null(missing.MinPrice);
        Assert.Null(missing.MaxPrice);
        Assert.Equal(0, negative.ActiveFilterCount);
    }

    [Theory]
    [InlineData("bad tag")]
    [InlineData("<script>")]
    [InlineData("---")]
    [InlineData("")]
    public void Normalize_rejects_tags_that_are_not_slugs(string tag)
    {
        var criteria = ProductListCriteria.Normalize(1, null, null, tag, null, null);

        Assert.Null(criteria.TagSlug);
    }

    [Fact]
    public void Normalize_caps_the_page_number()
    {
        var criteria = ProductListCriteria.Normalize(int.MaxValue, "name", null, null, null, null);

        Assert.Equal(ProductListCriteria.MaxPage, criteria.Page);
        Assert.Equal(ProductListSort.Name, criteria.Sort);
    }

    [Fact]
    public void Query_keeps_active_filters_and_omits_defaults()
    {
        var criteria = ProductListCriteria.Normalize(3, "price-desc", null, "Son", 10, 20);

        Assert.Equal("?sort=price-desc&tag=son&min=10&max=20&page=3", criteria.ForPage(3));
        Assert.Equal("?sort=price-desc&tag=son&min=10&max=20", criteria.ForPage(1));
        Assert.Equal("?sort=name&tag=son&min=10&max=20", criteria.ForSort(ProductListSort.Name));
        Assert.Equal("?sort=price-desc&min=10&max=20", criteria.ForTag(null));
        Assert.Equal("?sort=price-desc&tag=son", criteria.ForPrice(null, null));
        Assert.Equal("?sort=price-desc", criteria.WithoutTagOrPrice());
    }

    [Theory]
    [InlineData(2, 12, 30, 13, 24)]
    [InlineData(3, 12, 30, 25, 30)]
    [InlineData(1, 12, 0, 0, 0)]
    [InlineData(9, 12, 5, 0, 0)]
    public void ResultRange_describes_the_visible_slice(int page, int pageSize, int total, int start, int end)
    {
        Assert.Equal((start, end), ProductListCriteria.ResultRange(page, pageSize, total));
    }

    [Fact]
    public void PageWindow_inserts_ellipsis_outside_the_current_neighbourhood()
    {
        Assert.Equal(
            new int?[] { 1, null, 4, 5, 6, null, 10 },
            ProductListCriteria.PageWindow(5, 10));
        Assert.Equal(new int?[] { 1, 2, null, 10 }, ProductListCriteria.PageWindow(1, 10));
        Assert.Equal(new int?[] { 1 }, ProductListCriteria.PageWindow(1, 1));
        Assert.Empty(ProductListCriteria.PageWindow(1, 0));
    }
}
