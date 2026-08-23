using NewHarian.Application.Catalog;

namespace NewHarian.Application.Catalog;

public record AdminColorListItemDto(int Id, string NameVi, string? MeaningVi);

public interface IAdminColorService
{
    Task<IReadOnlyList<AdminColorListItemDto>> ListAsync(CancellationToken ct = default);
    Task<ColorDefinitionSaveRequest?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> SaveAsync(ColorDefinitionSaveRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<AdminColorDefinitionOptionDto>> GetOptionsAsync(CancellationToken ct = default);
}
