using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Cms;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Cms;

public sealed class CmsPageService(AppDbContext db) : ICmsPageService
{
    public async Task<PublicPageDto?> GetPublishedBySlugAsync(string slug, string lang, CancellationToken ct = default)
    {
        lang = NormalizeLang(lang);
        var page = await db.Pages.AsNoTracking()
            .Include(p => p.Translations)
            .Include(p => p.ContentBlocks).ThenInclude(b => b.Translations)
            .Include(p => p.ContentBlocks).ThenInclude(b => b.MediaFile)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);

        if (page is null) return null;

        var tr = Pick(page.Translations, lang);
        var blocks = page.ContentBlocks
            .Where(b => b.IsPublished)
            .OrderBy(b => b.SortOrder)
            .Select(b =>
            {
                var bt = Pick(b.Translations, lang);
                return new PublicBlockDto(
                    b.Id,
                    b.BlockType,
                    b.SortOrder,
                    bt?.Title,
                    bt?.Body,
                    b.MediaFile?.StoredPath,
                    b.LinkUrl,
                    b.ImagePosition,
                    b.ExtraData,
                    b.SpacingAfterRem);
            })
            .ToList();

        return new PublicPageDto(
            page.Id,
            page.Slug,
            page.ModuleCode,
            tr?.Title ?? page.Slug,
            tr?.HeroTitle,
            tr?.MetaTitle,
            tr?.MetaDescription,
            page.HeroImageUrl,
            blocks);
    }

    public async Task<IReadOnlyList<PublicMenuItemDto>> GetMenuItemsAsync(string menuCode, string lang, CancellationToken ct = default)
    {
        lang = NormalizeLang(lang);
        var menu = await db.Menus.AsNoTracking()
            .Include(m => m.Items).ThenInclude(i => i.Translations)
            .Include(m => m.Items).ThenInclude(i => i.Children).ThenInclude(c => c.Translations)
            .FirstOrDefaultAsync(m => m.Code == menuCode, ct);

        if (menu is null) return Array.Empty<PublicMenuItemDto>();

        return menu.Items
            .Where(i => i.IsActive && i.ParentId == null)
            .OrderBy(i => i.SortOrder)
            .Select(i => MapMenuItem(i, lang, activeOnly: true))
            .Where(i => i is not null)
            .Select(i => i!)
            .ToList();
    }

    public Task<IReadOnlyList<PublicMenuItemDto>> GetHeaderNavAsync(string lang, CancellationToken ct = default)
        => GetMenuItemsAsync("header-main", lang, ct);

    private static PublicMenuItemDto? MapMenuItem(MenuItem i, string lang, bool activeOnly)
    {
        if (activeOnly && !i.IsActive) return null;
        var t = Pick(i.Translations, lang);
        var children = i.Children
            .Where(c => !activeOnly || c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => MapMenuItem(c, lang, activeOnly))
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        // Dropdown parent with no visible children → hide
        if (string.Equals(i.ItemKey, "about", StringComparison.OrdinalIgnoreCase) && children.Count == 0)
            return null;

        return new PublicMenuItemDto(t?.Label ?? i.Url, i.Url, i.SortOrder, i.ItemKey, children);
    }

    public async Task<IReadOnlyList<PublicHomeSlideDto>> GetActiveHomeSlidesAsync(string lang, CancellationToken ct = default)
    {
        lang = NormalizeLang(lang);
        var slides = await db.HomeSlides.AsNoTracking()
            .Include(s => s.Translations)
            .Include(s => s.MediaFile)
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Take(5)
            .ToListAsync(ct);

        return slides
            .Select(s =>
            {
                var img = !string.IsNullOrWhiteSpace(s.ImageUrl) ? s.ImageUrl : s.MediaFile?.StoredPath;
                if (string.IsNullOrWhiteSpace(img)) return null;
                var cap = s.Translations.FirstOrDefault(t => t.LanguageCode == lang)?.Caption
                          ?? s.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Caption;
                return new PublicHomeSlideDto(img!, cap, s.LinkUrl, s.SortOrder);
            })
            .Where(x => x is not null)
            .Cast<PublicHomeSlideDto>()
            .ToList();
    }

    private static string NormalizeLang(string lang)
        => lang is "en" or "ja" ? lang : "vi";

    private static T? Pick<T>(IEnumerable<T> items, string lang) where T : class
    {
        if (typeof(T) == typeof(PageTranslation))
        {
            var list = items.Cast<PageTranslation>().ToList();
            return (list.FirstOrDefault(x => x.LanguageCode == lang)
                    ?? list.FirstOrDefault(x => x.LanguageCode == "vi")
                    ?? list.FirstOrDefault()) as T;
        }
        if (typeof(T) == typeof(ContentBlockTranslation))
        {
            var list = items.Cast<ContentBlockTranslation>().ToList();
            return (list.FirstOrDefault(x => x.LanguageCode == lang)
                    ?? list.FirstOrDefault(x => x.LanguageCode == "vi")
                    ?? list.FirstOrDefault()) as T;
        }
        if (typeof(T) == typeof(MenuItemTranslation))
        {
            var list = items.Cast<MenuItemTranslation>().ToList();
            return (list.FirstOrDefault(x => x.LanguageCode == lang)
                    ?? list.FirstOrDefault(x => x.LanguageCode == "vi")
                    ?? list.FirstOrDefault()) as T;
        }
        return items.FirstOrDefault();
    }
}

public sealed class AdminCmsService(AppDbContext db, IHtmlContentSanitizer html, ILogger<AdminCmsService> logger) : IAdminCmsService
{
    public async Task<IReadOnlyList<CmsPageListItem>> ListPagesAsync(string? moduleCode, CancellationToken ct = default)
    {
        var q = db.Pages.AsNoTracking().Include(p => p.Translations).AsQueryable();
        if (!string.IsNullOrWhiteSpace(moduleCode))
            q = q.Where(p => p.ModuleCode == moduleCode);

        var pages = await q.OrderBy(p => p.ModuleCode).ThenBy(p => p.Slug).ToListAsync(ct);
        return pages.Select(p =>
        {
            var vi = p.Translations.FirstOrDefault(t => t.LanguageCode == "vi");
            return new CmsPageListItem(p.Id, p.Slug, p.ModuleCode, p.IsPublished, p.UpdatedAt, vi?.Title ?? p.Slug);
        }).ToList();
    }

    public async Task<CmsPageDetailDto?> GetPageAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Pages.AsNoTracking().Include(x => x.Translations).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var vi = p.Translations.FirstOrDefault(t => t.LanguageCode == "vi");
        var en = p.Translations.FirstOrDefault(t => t.LanguageCode == "en");
        var ja = p.Translations.FirstOrDefault(t => t.LanguageCode == "ja");
        return new CmsPageDetailDto(
            p.Id, p.Slug, p.ModuleCode, p.IsPublished, p.HeroImageUrl,
            vi?.Title ?? "", vi?.HeroTitle, vi?.MetaTitle, vi?.MetaDescription,
            en?.Title ?? "", en?.HeroTitle, en?.MetaTitle, en?.MetaDescription,
            ja?.Title ?? "", ja?.HeroTitle, ja?.MetaTitle, ja?.MetaDescription);
    }

    public async Task<(bool Ok, string? Error)> SavePageMetaAsync(CmsPageSaveRequest model, CancellationToken ct = default)
    {
        logger.LogInformation("SavePageMeta Start Id={Id}", model.Id);
        try
        {
            if (string.IsNullOrWhiteSpace(model.TitleVi))
            {
                logger.LogWarning("SavePageMeta Done rejected Id={Id} Error={Error}", model.Id, "Tiêu đề tiếng Việt bắt buộc.");
                return (false, "Tiêu đề tiếng Việt bắt buộc.");
            }

            var page = await db.Pages.Include(p => p.Translations).FirstOrDefaultAsync(p => p.Id == model.Id, ct);
            if (page is null)
            {
                logger.LogWarning("SavePageMeta Done rejected Id={Id} Error={Error}", model.Id, "Không tìm thấy trang.");
                return (false, "Không tìm thấy trang.");
            }

            page.IsPublished = model.IsPublished;
            page.UpdatedAt = DateTime.UtcNow;

            // Preserve HeroImageUrl / HeroTitle (no longer edited in Admin Pages; Home may still fallback to them).
            UpsertPageTr(page, "vi", model.TitleVi, metaTitle: model.MetaTitleVi, metaDesc: model.MetaDescriptionVi);
            UpsertPageTr(page, "en", model.TitleEn, metaTitle: model.MetaTitleEn, metaDesc: model.MetaDescriptionEn);
            UpsertPageTr(page, "ja", model.TitleJa, metaTitle: model.MetaTitleJa, metaDesc: model.MetaDescriptionJa);

            await db.SaveChangesAsync(ct);
            logger.LogInformation("SavePageMeta Done Id={Id}", model.Id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SavePageMeta Error Id={Id}", model.Id);
            throw;
        }
    }

    public async Task<IReadOnlyList<CmsBlockListItem>> ListBlocksAsync(int pageId, CancellationToken ct = default)
    {
        var blocks = await db.ContentBlocks.AsNoTracking()
            .Include(b => b.Translations)
            .Include(b => b.MediaFile)
            .Where(b => b.PageId == pageId)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

        return blocks.Select(b =>
        {
            var vi = b.Translations.FirstOrDefault(t => t.LanguageCode == "vi");
            return new CmsBlockListItem(b.Id, b.BlockType, b.SortOrder, b.IsPublished, vi?.Title, b.MediaFile?.StoredPath, b.LinkUrl, b.ImagePosition);
        }).ToList();
    }

    public async Task<CmsBlockEditDto?> GetBlockAsync(int blockId, CancellationToken ct = default)
    {
        var b = await db.ContentBlocks.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.MediaFile)
            .FirstOrDefaultAsync(x => x.Id == blockId, ct);
        if (b is null) return null;
        var vi = b.Translations.FirstOrDefault(t => t.LanguageCode == "vi");
        var en = b.Translations.FirstOrDefault(t => t.LanguageCode == "en");
        var ja = b.Translations.FirstOrDefault(t => t.LanguageCode == "ja");
        return new CmsBlockEditDto(
            b.Id, b.PageId, b.BlockType, b.SortOrder, b.IsPublished,
            b.MediaFileId, b.MediaFile?.StoredPath, b.LinkUrl, b.ImagePosition, b.ExtraData,
            b.SpacingAfterRem,
            vi?.Title, vi?.Body, en?.Title, en?.Body, ja?.Title, ja?.Body);
    }

    public async Task<(bool Ok, string? Error, int? Id)> SaveBlockAsync(CmsBlockSaveRequest model, CancellationToken ct = default)
    {
        logger.LogInformation("SaveBlock Start Id={Id} PageId={PageId}", model.Id, model.PageId);
        try
        {
            if (!await db.Pages.AnyAsync(p => p.Id == model.PageId, ct))
            {
                logger.LogWarning("SaveBlock Done rejected PageId={PageId} Error={Error}", model.PageId, "Trang không tồn tại.");
                return (false, "Trang không tồn tại.", null);
            }

            var err = ValidateBlock(model);
            if (err is not null)
            {
                logger.LogWarning("SaveBlock Done rejected PageId={PageId} Error={Error}", model.PageId, err);
                return (false, err, null);
            }

            ContentBlock block;
            if (model.Id > 0)
            {
                block = await db.ContentBlocks.Include(b => b.Translations).FirstAsync(b => b.Id == model.Id, ct);
                if (block.PageId != model.PageId)
                {
                    logger.LogWarning("SaveBlock Done rejected Id={Id} Error={Error}", model.Id, "Block không thuộc trang.");
                    return (false, "Block không thuộc trang.", null);
                }
            }
            else
            {
                var maxOrder = await db.ContentBlocks.Where(b => b.PageId == model.PageId).Select(b => (int?)b.SortOrder).MaxAsync(ct) ?? 0;
                block = new ContentBlock
                {
                    PageId = model.PageId,
                    BlockType = model.BlockType,
                    SortOrder = maxOrder + 1
                };
                db.ContentBlocks.Add(block);
            }

            block.BlockType = model.BlockType;
            block.IsPublished = model.IsPublished;
            block.MediaFileId = model.MediaFileId;
            block.LinkUrl = string.IsNullOrWhiteSpace(model.LinkUrl) ? null : model.LinkUrl.Trim();
            block.ImagePosition = string.IsNullOrWhiteSpace(model.ImagePosition) ? null : model.ImagePosition.Trim();
            block.ExtraData = string.IsNullOrWhiteSpace(model.ExtraData) ? null : model.ExtraData.Trim();
            block.SpacingAfterRem = model.SpacingAfterRem;

            UpsertBlockTr(block, "vi", model.TitleVi, html.Sanitize(model.BodyVi));
            UpsertBlockTr(block, "en", model.TitleEn, html.Sanitize(model.BodyEn));
            UpsertBlockTr(block, "ja", model.TitleJa, html.Sanitize(model.BodyJa));

            var page = await db.Pages.FirstAsync(p => p.Id == model.PageId, ct);
            page.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveBlock Done Id={Id}", block.Id);
            return (true, null, block.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveBlock Error Id={Id} PageId={PageId}", model.Id, model.PageId);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteBlockAsync(int blockId, CancellationToken ct = default)
    {
        logger.LogInformation("DeleteBlock Start Id={Id}", blockId);
        try
        {
            var block = await db.ContentBlocks.FirstOrDefaultAsync(b => b.Id == blockId, ct);
            if (block is null)
            {
                logger.LogWarning("DeleteBlock Done rejected Id={Id} Error={Error}", blockId, "Không tìm thấy block.");
                return (false, "Không tìm thấy block.");
            }
            var pageId = block.PageId;
            db.ContentBlocks.Remove(block);
            var page = await db.Pages.FirstAsync(p => p.Id == pageId, ct);
            page.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("DeleteBlock Done Id={Id}", blockId);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteBlock Error Id={Id}", blockId);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> MoveBlockAsync(int blockId, int direction, CancellationToken ct = default)
    {
        logger.LogInformation("MoveBlock Start Id={Id} Direction={Direction}", blockId, direction);
        try
        {
            var block = await db.ContentBlocks.FirstOrDefaultAsync(b => b.Id == blockId, ct);
            if (block is null)
            {
                logger.LogWarning("MoveBlock Done rejected Id={Id} Error={Error}", blockId, "Không tìm thấy block.");
                return (false, "Không tìm thấy block.");
            }

            var siblings = await db.ContentBlocks.Where(b => b.PageId == block.PageId).OrderBy(b => b.SortOrder).ToListAsync(ct);
            var idx = siblings.FindIndex(b => b.Id == blockId);
            var swapIdx = idx + direction;
            if (swapIdx < 0 || swapIdx >= siblings.Count)
            {
                logger.LogInformation("MoveBlock Done Id={Id} skipped", blockId);
                return (true, null);
            }

            (siblings[idx].SortOrder, siblings[swapIdx].SortOrder) = (siblings[swapIdx].SortOrder, siblings[idx].SortOrder);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("MoveBlock Done Id={Id}", blockId);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveBlock Error Id={Id}", blockId);
            throw;
        }
    }

    private static string? ValidateBlock(CmsBlockSaveRequest model)
    {
        if (model.SpacingAfterRem < 0)
            return "Khoảng cách với block tiếp theo phải ≥ 0 (rem).";

        switch (model.BlockType)
        {
            case ContentBlockType.TextWithImage:
                if (model.MediaFileId is null or <= 0)
                    return "Vui lòng tải ảnh lên cho khối Văn bản và hình ảnh.";
                if (model.ImagePosition is not ("left" or "right"))
                    model.ImagePosition = "right";
                break;
            case ContentBlockType.CtaButton:
                if (string.IsNullOrWhiteSpace(model.TitleVi))
                    return "Nút liên kết cần chữ trên nút (tiếng Việt).";
                if (string.IsNullOrWhiteSpace(model.LinkUrl))
                    return "Nút liên kết cần đường dẫn (ví dụ /products hoặc https://...).";
                break;
            case ContentBlockType.DataTable:
                if (string.IsNullOrWhiteSpace(model.ExtraData))
                    return "DataTable cần ExtraData JSON rows.";
                try
                {
                    using var doc = JsonDocument.Parse(model.ExtraData);
                    if (!doc.RootElement.TryGetProperty("rows", out var rows) || rows.GetArrayLength() < 1)
                        return "DataTable cần ít nhất 1 dòng.";
                }
                catch
                {
                    return "ExtraData DataTable không hợp lệ.";
                }
                break;
            case ContentBlockType.RichText:
            case ContentBlockType.BulletList:
                if (string.IsNullOrWhiteSpace(model.BodyVi))
                    return "Body tiếng Việt bắt buộc.";
                break;
        }
        return null;
    }

    private static void UpsertPageTr(Page page, string lang, string title, string? metaTitle, string? metaDesc)
    {
        var tr = page.Translations.FirstOrDefault(t => t.LanguageCode == lang);
        if (tr is null)
        {
            tr = new PageTranslation { LanguageCode = lang };
            page.Translations.Add(tr);
        }
        tr.Title = title?.Trim() ?? "";
        tr.MetaTitle = string.IsNullOrWhiteSpace(metaTitle) ? null : metaTitle.Trim();
        tr.MetaDescription = string.IsNullOrWhiteSpace(metaDesc) ? null : metaDesc.Trim();
    }

    private static void UpsertBlockTr(ContentBlock block, string lang, string? title, string? body)
    {
        var tr = block.Translations.FirstOrDefault(t => t.LanguageCode == lang);
        if (tr is null)
        {
            tr = new ContentBlockTranslation { LanguageCode = lang };
            block.Translations.Add(tr);
        }
        tr.Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        tr.Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim();
    }
}
