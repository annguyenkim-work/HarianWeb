using NewHarian.Infrastructure.Catalog;

namespace NewHarian.UnitTests.Infrastructure.Catalog;

[Trait("Category", "Unit")]
public class ProductGalleryHelperTests
{
    [Fact]
    public void BuildGallerySlides_puts_main_first_then_distinct_variant_images()
    {
        var slides = ProductGalleryHelper.BuildGallerySlides(
            "/uploads/main.jpg",
            ["/uploads/red.jpg", null, "", "  ", "/uploads/main.jpg", "/uploads/blue.jpg", "/uploads/red.jpg"]);

        Assert.Equal(new[] { "/uploads/main.jpg", "/uploads/red.jpg", "/uploads/blue.jpg" }, slides);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildGallerySlides_without_main_image_uses_variants_only(string? main)
    {
        var slides = ProductGalleryHelper.BuildGallerySlides(main, ["/uploads/red.jpg"]);

        Assert.Equal(new[] { "/uploads/red.jpg" }, slides);
    }

    [Fact]
    public void BuildGallerySlides_is_empty_when_no_images()
    {
        Assert.Empty(ProductGalleryHelper.BuildGallerySlides(null, []));
    }

    [Theory]
    [InlineData("/uploads/blue.jpg", 2)]
    [InlineData("/uploads/red.jpg", 1)]
    [InlineData("/uploads/missing.jpg", 0)]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    public void ResolveSlideIndex_jumps_to_variant_image_or_main(string? variantImage, int expected)
    {
        var slides = new List<string> { "/uploads/main.jpg", "/uploads/red.jpg", "/uploads/blue.jpg" };

        Assert.Equal(expected, ProductGalleryHelper.ResolveSlideIndex(slides, "/uploads/main.jpg", variantImage));
    }

    [Fact]
    public void ResolveSlideIndex_falls_back_to_main_position_when_main_is_not_first()
    {
        var slides = new List<string> { "/uploads/red.jpg", "/uploads/main.jpg" };

        Assert.Equal(1, ProductGalleryHelper.ResolveSlideIndex(slides, "/uploads/main.jpg", "/uploads/missing.jpg"));
    }

    [Fact]
    public void ResolveSlideIndex_is_zero_for_empty_gallery()
    {
        Assert.Equal(0, ProductGalleryHelper.ResolveSlideIndex([], null, null));
    }
}
