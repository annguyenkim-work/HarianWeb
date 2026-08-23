using NewHarian.Domain.Entities;

namespace NewHarian.Application.Cms;

public interface IMenuAdminService
{
    Task<Menu?> GetHeaderMenuAsync(CancellationToken ct = default);
    Task<bool> SetActiveAsync(int itemId, bool isActive, CancellationToken ct = default);
    Task<bool> MoveItemAsync(int itemId, int direction, CancellationToken ct = default);
}
