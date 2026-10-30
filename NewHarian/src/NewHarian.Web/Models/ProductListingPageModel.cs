using NewHarian.Application.Catalog;

namespace NewHarian.Web.Models;

public sealed class ProductListingPageModel
{
    public CategoryCardDto? Category { get; init; }
    public required IReadOnlyList<ProductCardDto> Products { get; init; }
    public required ProductListCriteria Criteria { get; init; }
    public int Total { get; init; }
    public required IReadOnlyList<CategoryCardDto> Categories { get; init; }
    public required IReadOnlyList<TagChipDto> Tags { get; init; }

    public string BasePath => Category is null
        ? "/products"
        : "/products/" + Uri.EscapeDataString(Category.Slug);

    public int TotalPages => Total <= 0
        ? 0
        : (int)Math.Ceiling(Total / (double)ProductListCriteria.PageSize);

    public string Href(string query) => BasePath + query;

    public string PathForCategory(string? slug)
    {
        var path = string.IsNullOrWhiteSpace(slug)
            ? "/products"
            : "/products/" + Uri.EscapeDataString(slug);
        return path + Criteria.ForPage(1);
    }
}
