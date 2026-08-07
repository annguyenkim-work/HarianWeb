using NewHarian.Domain.Enums;

namespace NewHarian.Application.Posts;

public record SitePostListItemDto(
    int Id,
    PostKind Kind,
    string Slug,
    string Title,
    string? Excerpt,
    string? CoverImageUrl,
    DateTime? PublishedAt,
    bool IsPublished,
    int SortOrder);

public record SitePostDetailDto(
    int Id,
    PostKind Kind,
    string Slug,
    string Title,
    string? Excerpt,
    string? Body,
    string? CoverImageUrl,
    DateTime? PublishedAt);

public record AdminSitePostEditDto(
    int Id,
    PostKind Kind,
    string Slug,
    bool IsPublished,
    DateTime? PublishedAt,
    int SortOrder,
    int? CoverImageMediaFileId,
    string? CoverImageUrl,
    string TitleVi, string? ExcerptVi, string? BodyVi,
    string TitleEn, string? ExcerptEn, string? BodyEn,
    string TitleJa, string? ExcerptJa, string? BodyJa);

public class SitePostSaveRequest
{
    public int? Id { get; set; }
    public PostKind Kind { get; set; }
    public string Slug { get; set; } = "";
    public bool IsPublished { get; set; }
    public int SortOrder { get; set; }
    public int? CoverImageMediaFileId { get; set; }
    public string? CoverImageUrl { get; set; }
    public string TitleVi { get; set; } = "";
    public string? ExcerptVi { get; set; }
    public string? BodyVi { get; set; }
    public string TitleEn { get; set; } = "";
    public string? ExcerptEn { get; set; }
    public string? BodyEn { get; set; }
    public string TitleJa { get; set; } = "";
    public string? ExcerptJa { get; set; }
    public string? BodyJa { get; set; }
}

public interface ISitePostService
{
    Task<IReadOnlyList<SitePostListItemDto>> ListPublishedAsync(PostKind kind, string lang, CancellationToken ct = default);
    Task<SitePostDetailDto?> GetPublishedBySlugAsync(PostKind kind, string slug, string lang, CancellationToken ct = default);
}

public interface IAdminSitePostService
{
    Task<IReadOnlyList<SitePostListItemDto>> ListAsync(PostKind kind, CancellationToken ct = default);
    Task<AdminSitePostEditDto?> GetAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> SaveAsync(SitePostSaveRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct = default);
    Task MoveAsync(int id, int direction, CancellationToken ct = default);
    Task<IReadOnlyList<SitePostListItemDto>> ListJobOptionsAsync(CancellationToken ct = default);
}
