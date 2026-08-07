using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Posts;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Posts;

public sealed class SitePostService(AppDbContext db) : ISitePostService
{
    public async Task<IReadOnlyList<SitePostListItemDto>> ListPublishedAsync(PostKind kind, string lang, CancellationToken ct = default)
    {
        lang = Normalize(lang);
        var posts = await db.SitePosts.AsNoTracking()
            .Include(p => p.Translations)
            .Include(p => p.CoverImage)
            .Where(p => p.Kind == kind && p.IsPublished)
            .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        return posts.Select(p => ToListItem(p, lang)).ToList();
    }

    public async Task<SitePostDetailDto?> GetPublishedBySlugAsync(PostKind kind, string slug, string lang, CancellationToken ct = default)
    {
        lang = Normalize(lang);
        var p = await db.SitePosts.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.CoverImage)
            .FirstOrDefaultAsync(x => x.Kind == kind && x.Slug == slug && x.IsPublished, ct);
        if (p is null) return null;
        var t = Pick(p.Translations, lang);
        return new SitePostDetailDto(
            p.Id, p.Kind, p.Slug,
            t?.Title ?? p.Slug,
            t?.Excerpt,
            t?.Body,
            p.CoverImage?.StoredPath,
            p.PublishedAt);
    }

    internal static SitePostListItemDto ToListItem(SitePost p, string lang)
    {
        var t = Pick(p.Translations, lang);
        return new SitePostListItemDto(
            p.Id, p.Kind, p.Slug,
            t?.Title ?? p.Slug,
            t?.Excerpt,
            p.CoverImage?.StoredPath,
            p.PublishedAt,
            p.IsPublished,
            p.SortOrder);
    }

    internal static string Normalize(string lang) => lang is "en" or "ja" ? lang : "vi";

    internal static SitePostTranslation? Pick(IEnumerable<SitePostTranslation> items, string lang)
        => items.FirstOrDefault(t => t.LanguageCode == lang)
           ?? items.FirstOrDefault(t => t.LanguageCode == "vi")
           ?? items.FirstOrDefault();
}

public sealed class AdminSitePostService(AppDbContext db, IHtmlContentSanitizer html, ILogger<AdminSitePostService> logger) : IAdminSitePostService
{
    public async Task<IReadOnlyList<SitePostListItemDto>> ListAsync(PostKind kind, CancellationToken ct = default)
    {
        var posts = await db.SitePosts.AsNoTracking()
            .Include(p => p.Translations)
            .Include(p => p.CoverImage)
            .Where(p => p.Kind == kind)
            .OrderBy(p => p.SortOrder).ThenByDescending(p => p.Id)
            .ToListAsync(ct);
        return posts.Select(p => SitePostService.ToListItem(p, "vi")).ToList();
    }

    public async Task<IReadOnlyList<SitePostListItemDto>> ListJobOptionsAsync(CancellationToken ct = default)
        => await ListAsync(PostKind.Job, ct);

    public async Task<AdminSitePostEditDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var p = await db.SitePosts.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.CoverImage)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var vi = Pick(p.Translations, "vi");
        var en = Pick(p.Translations, "en");
        var ja = Pick(p.Translations, "ja");
        return new AdminSitePostEditDto(
            p.Id, p.Kind, p.Slug, p.IsPublished, p.PublishedAt, p.SortOrder,
            p.CoverImageMediaFileId, p.CoverImage?.StoredPath,
            vi?.Title ?? "", vi?.Excerpt, vi?.Body,
            en?.Title ?? "", en?.Excerpt, en?.Body,
            ja?.Title ?? "", ja?.Excerpt, ja?.Body);
    }

    public async Task<(bool Ok, string? Error, int? Id)> SaveAsync(SitePostSaveRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("SaveSitePost Start Id={Id} Kind={Kind}", request.Id, request.Kind);
        try
        {
            if (string.IsNullOrWhiteSpace(request.TitleVi))
            {
                logger.LogWarning("SaveSitePost Done rejected Error={Error}", "Tiêu đề tiếng Việt bắt buộc.");
                return (false, "Tiêu đề tiếng Việt bắt buộc.", null);
            }
            if (string.IsNullOrWhiteSpace(request.BodyVi))
            {
                logger.LogWarning("SaveSitePost Done rejected Error={Error}", "Nội dung tiếng Việt bắt buộc.");
                return (false, "Nội dung tiếng Việt bắt buộc.", null);
            }

            SitePost entity;
            if (request.Id is int id)
            {
                entity = await db.SitePosts.Include(p => p.Translations).FirstOrDefaultAsync(p => p.Id == id, ct)
                         ?? throw new InvalidOperationException("Post not found");
                if (entity.Kind != request.Kind)
                {
                    logger.LogWarning("SaveSitePost Done rejected Id={Id} Error={Error}", id, "Không đổi loại bài viết.");
                    return (false, "Không đổi loại bài viết.", null);
                }
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var baseSlug = string.IsNullOrWhiteSpace(request.Slug)
                    ? Application.Abstractions.SlugHelper.FromVietnamese(request.TitleVi)
                    : request.Slug.Trim();
                if (string.IsNullOrWhiteSpace(baseSlug))
                {
                    logger.LogWarning("SaveSitePost Done rejected Error={Error}", "Không tạo được slug.");
                    return (false, "Không tạo được slug.", null);
                }

                var slug = await EnsureUniqueSlugAsync(request.Kind, baseSlug, null, ct);
                var maxOrder = await db.SitePosts.Where(p => p.Kind == request.Kind)
                    .MaxAsync(p => (int?)p.SortOrder, ct) ?? 0;
                entity = new SitePost
                {
                    Kind = request.Kind,
                    Slug = slug,
                    SortOrder = maxOrder + 1,
                    CreatedAt = DateTime.UtcNow
                };
                db.SitePosts.Add(entity);
            }

            var wasPublished = entity.IsPublished;
            entity.IsPublished = request.IsPublished;
            if (request.IsPublished && (!wasPublished || entity.PublishedAt is null))
                entity.PublishedAt = DateTime.UtcNow;
            if (!request.IsPublished)
                entity.PublishedAt = entity.PublishedAt; // keep historical publish date if any

            entity.CoverImageMediaFileId = request.CoverImageMediaFileId is > 0 ? request.CoverImageMediaFileId : null;

            Upsert(entity, "vi", request.TitleVi, request.ExcerptVi, html.Sanitize(request.BodyVi));
            Upsert(entity, "en",
                string.IsNullOrWhiteSpace(request.TitleEn) ? request.TitleVi : request.TitleEn,
                request.ExcerptEn, html.Sanitize(request.BodyEn));
            Upsert(entity, "ja",
                string.IsNullOrWhiteSpace(request.TitleJa) ? request.TitleVi : request.TitleJa,
                request.ExcerptJa, html.Sanitize(request.BodyJa));

            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveSitePost Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveSitePost Error Id={Id}", request.Id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct = default)
    {
        logger.LogInformation("DeleteSitePost Start Id={Id}", id);
        try
        {
            var p = await db.SitePosts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null)
            {
                logger.LogWarning("DeleteSitePost Done rejected Id={Id} Error={Error}", id, "Không tìm thấy.");
                return (false, "Không tìm thấy.");
            }
            if (await db.JobApplications.AnyAsync(a => a.SitePostId == id, ct))
            {
                logger.LogWarning("DeleteSitePost Done rejected Id={Id} Error={Error}", id, "Bài đã có hồ sơ ứng tuyển - hãy Archive (bỏ publish) thay vì xóa.");
                return (false, "Bài đã có hồ sơ ứng tuyển - hãy Archive (bỏ publish) thay vì xóa.");
            }
            db.SitePosts.Remove(p);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("DeleteSitePost Done Id={Id}", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteSitePost Error Id={Id}", id);
            throw;
        }
    }

    public async Task MoveAsync(int id, int direction, CancellationToken ct = default)
    {
        logger.LogInformation("MoveSitePost Start Id={Id} Direction={Direction}", id, direction);
        try
        {
            var post = await db.SitePosts.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (post is null)
            {
                logger.LogInformation("MoveSitePost Done Id={Id} skipped", id);
                return;
            }
            var siblings = await db.SitePosts.Where(p => p.Kind == post.Kind).OrderBy(p => p.SortOrder).ToListAsync(ct);
            var idx = siblings.FindIndex(p => p.Id == id);
            var swap = idx + direction;
            if (swap < 0 || swap >= siblings.Count)
            {
                logger.LogInformation("MoveSitePost Done Id={Id} skipped", id);
                return;
            }
            (siblings[idx].SortOrder, siblings[swap].SortOrder) = (siblings[swap].SortOrder, siblings[idx].SortOrder);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("MoveSitePost Done Id={Id}", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveSitePost Error Id={Id}", id);
            throw;
        }
    }

    private async Task<string> EnsureUniqueSlugAsync(PostKind kind, string baseSlug, int? excludeId, CancellationToken ct)
    {
        var slug = baseSlug;
        var i = 2;
        while (await db.SitePosts.AnyAsync(p => p.Kind == kind && p.Slug == slug && p.Id != (excludeId ?? 0), ct))
        {
            slug = $"{baseSlug}-{i}";
            i++;
        }
        return slug;
    }

    private static void Upsert(SitePost post, string lang, string title, string? excerpt, string? body)
    {
        var tr = post.Translations.FirstOrDefault(t => t.LanguageCode == lang);
        if (tr is null)
        {
            tr = new SitePostTranslation { LanguageCode = lang };
            post.Translations.Add(tr);
        }
        tr.Title = title.Trim();
        tr.Excerpt = string.IsNullOrWhiteSpace(excerpt) ? null : excerpt.Trim();
        tr.Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim();
    }

    private static SitePostTranslation? Pick(IEnumerable<SitePostTranslation> items, string lang)
        => items.FirstOrDefault(t => t.LanguageCode == lang)
           ?? items.FirstOrDefault(t => t.LanguageCode == "vi")
           ?? items.FirstOrDefault();
}
