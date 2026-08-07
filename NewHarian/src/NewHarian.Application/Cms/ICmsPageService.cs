using NewHarian.Domain.Enums;

namespace NewHarian.Application.Cms;

public record CmsPageListItem(int Id, string Slug, string ModuleCode, bool IsPublished, DateTime? UpdatedAt, string TitleVi);

public record CmsPageDetailDto(
    int Id,
    string Slug,
    string ModuleCode,
    bool IsPublished,
    string? HeroImageUrl,
    string TitleVi, string? HeroTitleVi, string? MetaTitleVi, string? MetaDescriptionVi,
    string TitleEn, string? HeroTitleEn, string? MetaTitleEn, string? MetaDescriptionEn,
    string TitleJa, string? HeroTitleJa, string? MetaTitleJa, string? MetaDescriptionJa);

public record CmsBlockListItem(
    int Id,
    ContentBlockType BlockType,
    int SortOrder,
    bool IsPublished,
    string? TitleVi,
    string? ImageUrl,
    string? LinkUrl,
    string? ImagePosition);

public record CmsBlockEditDto(
    int Id,
    int PageId,
    ContentBlockType BlockType,
    int SortOrder,
    bool IsPublished,
    int? MediaFileId,
    string? ImageUrl,
    string? LinkUrl,
    string? ImagePosition,
    string? ExtraData,
    decimal SpacingAfterRem,
    string? TitleVi, string? BodyVi,
    string? TitleEn, string? BodyEn,
    string? TitleJa, string? BodyJa);

public class CmsPageSaveRequest
{
    public int Id { get; set; }
    public bool IsPublished { get; set; }
    public string? HeroImageUrl { get; set; }
    public string TitleVi { get; set; } = "";
    public string? HeroTitleVi { get; set; }
    public string? MetaTitleVi { get; set; }
    public string? MetaDescriptionVi { get; set; }
    public string TitleEn { get; set; } = "";
    public string? HeroTitleEn { get; set; }
    public string? MetaTitleEn { get; set; }
    public string? MetaDescriptionEn { get; set; }
    public string TitleJa { get; set; } = "";
    public string? HeroTitleJa { get; set; }
    public string? MetaTitleJa { get; set; }
    public string? MetaDescriptionJa { get; set; }
}

public class CmsBlockSaveRequest
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public ContentBlockType BlockType { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public int? MediaFileId { get; set; }
    public string? LinkUrl { get; set; }
    public string? ImagePosition { get; set; }
    public string? ExtraData { get; set; }
    /// <summary>Gap to next block in rem; default 0.35, min 0.</summary>
    public decimal SpacingAfterRem { get; set; } = 0.35m;
    public string? TitleVi { get; set; }
    public string? BodyVi { get; set; }
    public string? TitleEn { get; set; }
    public string? BodyEn { get; set; }
    public string? TitleJa { get; set; }
    public string? BodyJa { get; set; }
}

/// <summary>Public published page with localized content.</summary>
public record PublicPageDto(
    int Id,
    string Slug,
    string ModuleCode,
    string Title,
    string? HeroTitle,
    string? MetaTitle,
    string? MetaDescription,
    string? HeroImageUrl,
    IReadOnlyList<PublicBlockDto> Blocks);

public record PublicBlockDto(
    int Id,
    ContentBlockType BlockType,
    int SortOrder,
    string? Title,
    string? Body,
    string? ImageUrl,
    string? LinkUrl,
    string? ImagePosition,
    string? ExtraData,
    decimal SpacingAfterRem);

public record PublicMenuItemDto(string Label, string Url, int SortOrder, string ItemKey = "", IReadOnlyList<PublicMenuItemDto>? Children = null);

public record PublicHomeSlideDto(string ImageUrl, string? Caption, string? LinkUrl, int SortOrder);

public interface ICmsPageService
{
    Task<PublicPageDto?> GetPublishedBySlugAsync(string slug, string lang, CancellationToken ct = default);
    Task<IReadOnlyList<PublicMenuItemDto>> GetMenuItemsAsync(string menuCode, string lang, CancellationToken ct = default);
    Task<IReadOnlyList<PublicMenuItemDto>> GetHeaderNavAsync(string lang, CancellationToken ct = default);
    Task<IReadOnlyList<PublicHomeSlideDto>> GetActiveHomeSlidesAsync(string lang, CancellationToken ct = default);
}

public interface IAdminCmsService
{
    Task<IReadOnlyList<CmsPageListItem>> ListPagesAsync(string? moduleCode, CancellationToken ct = default);
    Task<CmsPageDetailDto?> GetPageAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SavePageMetaAsync(CmsPageSaveRequest model, CancellationToken ct = default);
    Task<IReadOnlyList<CmsBlockListItem>> ListBlocksAsync(int pageId, CancellationToken ct = default);
    Task<CmsBlockEditDto?> GetBlockAsync(int blockId, CancellationToken ct = default);
    Task<(bool Ok, string? Error, int? Id)> SaveBlockAsync(CmsBlockSaveRequest model, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeleteBlockAsync(int blockId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> MoveBlockAsync(int blockId, int direction, CancellationToken ct = default);
}
