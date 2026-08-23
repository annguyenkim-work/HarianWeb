namespace NewHarian.Application.Catalog;

/// <summary>Admin physical products (CatalogKind.Product) + SKU suggest.</summary>
public interface IAdminProductService
{
    Task<IReadOnlyList<AdminProductListItemDto>> ListProductsAsync(int? categoryId, CancellationToken ct = default);
    Task<AdminProductEditDto?> GetProductAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> SaveProductAsync(ProductSaveRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeleteProductAsync(int id, CancellationToken ct = default);
    Task MoveProductAsync(int id, int direction, CancellationToken ct = default);

    /// <summary>SKU autocomplete for Thêm đơn, Nhập kho, …</summary>
    Task<IReadOnlyList<VariantSuggestDto>> SuggestVariantsAsync(string? q, int take = 15, CancellationToken ct = default);

    /// <summary>Display label for forms after validation fail.</summary>
    Task<string?> GetVariantDisplayAsync(int variantId, CancellationToken ct = default);
}

/// <summary>Shared product-variant pick list for Admin autocomplete.</summary>
public record VariantSuggestDto(
    int Id,
    string Sku,
    string ProductName,
    string VariantLabel,
    decimal Price,
    string Display);
