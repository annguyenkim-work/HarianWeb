using NewHarian.Domain.Entities;

namespace NewHarian.Application.Cms;

public sealed record HomeSlideImageUpload(
    Stream Content,
    string FileName,
    string ContentType,
    string? UploadedByUserId);

public interface IHomeSlideAdminService
{
    Task<IReadOnlyList<HomeSlide>> ListAsync(CancellationToken ct = default);
    Task CreateAsync(
        string? captionVi,
        string? linkUrl,
        bool isActive,
        HomeSlideImageUpload? image,
        CancellationToken ct = default);
    Task MoveAsync(int id, int direction, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
