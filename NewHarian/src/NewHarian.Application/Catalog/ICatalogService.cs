using NewHarian.Domain.Enums;

namespace NewHarian.Application.Catalog;

public record CategoryCardDto(
    int Id,
    string Slug,
    string Name,
    string? ImageUrl,
    int ProductCount,
    int PhysicalCount = 0,
    int ServiceCount = 0);
public record ProductCardDto(
    int Id,
    string CategorySlug,
    string Slug,
    string Name,
    CatalogKind Kind,
    decimal? FromPrice,
    string? ImageUrl,
    bool HidePrice = false,
    string? ShortDescription = null);

public record ProductVariantDto(
    int Id,
    string Sku,
    string Label,
    decimal Price,
    bool IsDefault,
    string? ColorMeaning,
    string? ImageUrl,
    int GallerySlideIndex,
    string? SizeLabel = null,
    string? ColorName = null);

public record ProductDetailDto(
    int Id,
    string CategorySlug,
    string CategoryName,
    string Slug,
    string Name,
    string? ShortDescription,
    string? Description,
    CatalogKind Kind,
    bool HidePrice,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<ProductCardDto> Related);

public record TagChipDto(string Slug, string Name);

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryCardDto>> GetActiveCategoriesAsync(string lang, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryCardDto>> GetHomeCategoriesAsync(string lang, CancellationToken ct = default);
    Task<IReadOnlyList<ProductCardDto>> GetFeaturedProductsAsync(string lang, int take = 6, CancellationToken ct = default);
    Task<CategoryCardDto?> GetCategoryAsync(string categorySlug, string lang, CancellationToken ct = default);
    Task<(IReadOnlyList<ProductCardDto> Items, int Total)> GetProductsByCategoryAsync(
        string categorySlug, string lang, int page, int pageSize, CatalogKind kind, CancellationToken ct = default);
    /// <summary>Published items filtered by CatalogKind (Product=0, Service=1), queried from the matching table.</summary>
    Task<(IReadOnlyList<ProductCardDto> Items, int Total)> GetProductsByTypeAsync(
        CatalogKind kind, string lang, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Searches across both Products and Services; optional tag filter applies to physical products only.</summary>
    Task<(IReadOnlyList<ProductCardDto> Items, int Total)> SearchProductsAsync(
        string query, string lang, int page, int pageSize, string? tagSlug = null, CancellationToken ct = default);
    Task<IReadOnlyList<TagChipDto>> GetPublishedProductTagsAsync(CancellationToken ct = default);
    Task<ProductDetailDto?> GetProductAsync(string categorySlug, string productSlug, string lang, CancellationToken ct = default);
    Task<ProductDetailDto?> GetServiceAsync(string categorySlug, string productSlug, string lang, CancellationToken ct = default);
    /// <summary>
    /// Resolve variant by id within the given catalog kind.
    /// ProductVariants and ServiceVariants are separate identity sequences — never look up across kinds.
    /// </summary>
    Task<ProductVariantDto?> GetVariantAsync(int variantId, string lang, CatalogKind kind, CancellationToken ct = default);
}
