namespace NewHarian.Application.Catalog;

/// <summary>Admin service offerings (CatalogKind.Service).</summary>
public interface IAdminServiceOfferingService
{
    Task<IReadOnlyList<AdminProductListItemDto>> ListServicesAsync(int? categoryId, CancellationToken ct = default);
    Task<AdminProductEditDto?> GetServiceAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> SaveServiceAsync(ProductSaveRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeleteServiceAsync(int id, CancellationToken ct = default);
    Task MoveServiceAsync(int id, int direction, CancellationToken ct = default);
}
