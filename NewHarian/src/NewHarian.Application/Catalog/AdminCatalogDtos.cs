using NewHarian.Domain.Enums;

namespace NewHarian.Application.Catalog;

public record AdminCategoryListItemDto(int Id, string Slug, string NameVi, int SortOrder, bool IsActive, bool ShowOnHome, string? ImageUrl, int ProductCount);

public record AdminCategoryEditDto(
    int Id,
    string Slug,
    int SortOrder,
    bool IsActive,
    bool ShowOnHome,
    string? ImageUrl,
    string NameVi,
    string? DescVi,
    string NameEn,
    string? DescEn,
    string NameJa,
    string? DescJa);

public record AdminProductListItemDto(
    int Id,
    int CategoryId,
    string CategorySlug,
    string Slug,
    string NameVi,
    CatalogKind Kind,
    ProductStatus Status,
    int VariantCount,
    decimal? FromPrice);

public record AdminVariantEditDto(
    int Id,
    string Sku,
    string VariantLabel,
    int? ColorDefinitionId,
    decimal Price,
    bool IsDefault,
    int SortOrder,
    bool IsActive,
    int? ImageMediaFileId,
    string? ImageUrl,
    int StockQuantity = 0);

public record AdminColorDefinitionOptionDto(int Id, string NameVi);

public record AdminCategoryOptionDto(int Id, string Name);

public record AdminProductEditDto(
    int Id,
    int CategoryId,
    string Slug,
    CatalogKind Kind,
    ProductStatus Status,
    bool IsFeatured,
    int SortOrder,
    bool HasVariantSize,
    bool HasVariantColor,
    bool HidePrice,
    int? MainImageMediaFileId,
    string? MainImageUrl,
    string NameVi,
    string? ShortVi,
    string? DescVi,
    string NameEn,
    string? ShortEn,
    string? DescEn,
    string NameJa,
    string? ShortJa,
    string? DescJa,
    IReadOnlyList<AdminVariantEditDto> Variants,
    string TagsCsv = "");

public class CategorySaveRequest
{
    public int? Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool ShowOnHome { get; set; }
    public string? ImageUrl { get; set; }
    public string NameVi { get; set; } = string.Empty;
    public string? DescVi { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string? DescEn { get; set; }
    public string NameJa { get; set; } = string.Empty;
    public string? DescJa { get; set; }
}

public class VariantSaveRequest
{
    public int? Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public int? ColorDefinitionId { get; set; }
    public decimal Price { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public int? ImageMediaFileId { get; set; }
    /// <summary>Display-only; not persisted (source of truth is StockLots).</summary>
    public string? ImageUrl { get; set; }
    /// <summary>Display-only cache total; managed via Kho.</summary>
    public int StockQuantity { get; set; }
}

/// <summary>Shared save request for both Products and Services; Kind decides target table.</summary>
public class ProductSaveRequest
{
    public int? Id { get; set; }
    public int CategoryId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public CatalogKind Kind { get; set; } = CatalogKind.Product;
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public bool HasVariantSize { get; set; }
    public bool HasVariantColor { get; set; }
    /// <summary>Hide prices on guest UI (no fixed price / book only). Services only — ignored for Products.</summary>
    public bool HidePrice { get; set; }
    public int? MainImageMediaFileId { get; set; }
    /// <summary>Display-only; not persisted.</summary>
    public string? MainImageUrl { get; set; }
    public string NameVi { get; set; } = string.Empty;
    public string? ShortVi { get; set; }
    public string? DescVi { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string? ShortEn { get; set; }
    public string? DescEn { get; set; }
    public string NameJa { get; set; } = string.Empty;
    public string? ShortJa { get; set; }
    public string? DescJa { get; set; }
    /// <summary>Comma/semicolon-separated tag names (physical products only).</summary>
    public string? TagsCsv { get; set; }
    public List<VariantSaveRequest> Variants { get; set; } = [];
}
