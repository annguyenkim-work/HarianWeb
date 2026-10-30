namespace NewHarian.Application.Hr;

public sealed record JobPositionListItemDto(int Id, string Name, int SortOrder, bool IsActive);

public sealed record JobPositionOptionDto(int Id, string Name, bool IsActive);

public sealed class JobPositionForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public interface IAdminJobPositionService
{
    Task<IReadOnlyList<JobPositionListItemDto>> ListAsync(CancellationToken ct = default);

    Task<JobPositionForm?> GetAsync(int id, CancellationToken ct = default);

    Task<(bool Ok, string? Error, int? Id)> SaveAsync(JobPositionForm form, CancellationToken ct = default);

    Task MoveAsync(int id, int direction, CancellationToken ct = default);

    /// <summary>Active positions. <paramref name="includeId"/> stays in the list when that position is off.</summary>
    Task<IReadOnlyList<JobPositionOptionDto>> ListChoicesAsync(int? includeId, CancellationToken ct = default);
}
