using NewHarian.Domain.Enums;

namespace NewHarian.Application.Catalog;

public interface IAdminCategoryService
{
    Task<IReadOnlyList<AdminCategoryListItemDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<AdminCategoryEditDto?> GetCategoryAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> SaveCategoryAsync(CategorySaveRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeleteCategoryAsync(int id, CancellationToken ct = default);
    Task MoveCategoryAsync(int id, int direction, CancellationToken ct = default);
    Task<IReadOnlyList<AdminCategoryOptionDto>> GetCategoryOptionsAsync(CancellationToken ct = default);
}
