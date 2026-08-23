using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Catalog;

public class AdminCategoryService(AppDbContext db, ILogger<AdminCategoryService> logger)
    : IAdminCategoryService
{
    public async Task<IReadOnlyList<AdminCategoryListItemDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var list = await db.Categories.AsNoTracking()
            .Include(c => c.Translations)
            .Include(c => c.Products)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        return list.Select(c => new AdminCategoryListItemDto(
            c.Id,
            c.Slug,
            c.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? c.Slug,
            c.SortOrder,
            c.IsActive,
            c.ShowOnHome,
            c.ImageUrl,
            c.Products.Count)).ToList();
    }

    public async Task<AdminCategoryEditDto?> GetCategoryAsync(int id, CancellationToken ct = default)
    {
        var c = await db.Categories.AsNoTracking()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        return new AdminCategoryEditDto(
            c.Id, c.Slug, c.SortOrder, c.IsActive, c.ShowOnHome, c.ImageUrl,
            Pick(c.Translations, "vi")?.Name ?? "", Pick(c.Translations, "vi")?.Description,
            Pick(c.Translations, "en")?.Name ?? "", Pick(c.Translations, "en")?.Description,
            Pick(c.Translations, "ja")?.Name ?? "", Pick(c.Translations, "ja")?.Description);
    }

    public async Task<(bool Ok, string? Error, int? Id)> SaveCategoryAsync(
        CategorySaveRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("SaveCategory Start Id={Id}", request.Id);
        try
        {
            if (string.IsNullOrWhiteSpace(request.NameVi))
                return RejectSaveCategory("Tên tiếng Việt bắt buộc.");
            if (string.IsNullOrWhiteSpace(request.NameEn))
                return RejectSaveCategory("Tên tiếng Anh bắt buộc.");
            if (string.IsNullOrWhiteSpace(request.NameJa))
                return RejectSaveCategory("Tên tiếng Nhật bắt buộc.");

            Category entity;
            if (request.Id is int id)
            {
                entity = await db.Categories.Include(c => c.Translations)
                    .FirstOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new InvalidOperationException("Category not found");
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var baseSlug = SlugHelper.FromVietnamese(request.NameVi);
                if (string.IsNullOrWhiteSpace(baseSlug))
                    return RejectSaveCategory("Không tạo được slug từ tên tiếng Việt.");

                var slug = await EnsureUniqueCategorySlugAsync(baseSlug, null, ct);
                var maxOrder = await db.Categories.MaxAsync(c => (int?)c.SortOrder, ct) ?? 0;
                entity = new Category
                {
                    CreatedAt = DateTime.UtcNow,
                    Slug = slug,
                    SortOrder = maxOrder + 1
                };
                db.Categories.Add(entity);
            }

            entity.IsActive = request.IsActive;
            entity.ShowOnHome = request.ShowOnHome;
            entity.ImageUrl = request.ImageUrl;
            UpsertCatTranslation(entity, "vi", request.NameVi.Trim(), request.DescVi);
            UpsertCatTranslation(entity, "en", request.NameEn.Trim(), request.DescEn);
            UpsertCatTranslation(entity, "ja", request.NameJa.Trim(), request.DescJa);

            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveCategory Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveCategory Error Id={Id}", request.Id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        logger.LogInformation("DeleteCategory Start Id={Id}", id);
        try
        {
            var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                logger.LogWarning("DeleteCategory Done rejected Id={Id} Error={Error}", id, "Không tìm thấy.");
                return (false, "Không tìm thấy.");
            }

            if (!c.IsActive)
            {
                logger.LogInformation("DeleteCategory Done Id={Id} already inactive", id);
                return (true, null);
            }

            c.IsActive = false;
            c.ShowOnHome = false;
            c.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("DeleteCategory Done Id={Id} soft-deactivated", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteCategory Error Id={Id}", id);
            throw;
        }
    }

    public async Task MoveCategoryAsync(int id, int direction, CancellationToken ct = default)
    {
        logger.LogInformation("MoveCategory Start Id={Id} Direction={Direction}", id, direction);
        try
        {
            var items = await db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync(ct);
            var idx = items.FindIndex(c => c.Id == id);
            var swapIdx = idx + direction;
            if (idx < 0 || swapIdx < 0 || swapIdx >= items.Count)
            {
                logger.LogInformation("MoveCategory Done Id={Id} skipped", id);
                return;
            }

            (items[idx].SortOrder, items[swapIdx].SortOrder) = (items[swapIdx].SortOrder, items[idx].SortOrder);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("MoveCategory Done Id={Id}", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveCategory Error Id={Id}", id);
            throw;
        }
    }

    public async Task<IReadOnlyList<AdminCategoryOptionDto>> GetCategoryOptionsAsync(CancellationToken ct = default)
    {
        var cats = await db.Categories.AsNoTracking()
            .Include(c => c.Translations)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        return cats.Select(c => new AdminCategoryOptionDto(
                c.Id,
                c.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? c.Slug))
            .ToList();
    }

    private (bool Ok, string? Error, int? Id) RejectSaveCategory(string error)
    {
        logger.LogWarning("SaveCategory Done rejected Error={Error}", error);
        return (false, error, null);
    }

    private async Task<string> EnsureUniqueCategorySlugAsync(
        string baseSlug,
        int? excludeId,
        CancellationToken ct)
    {
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != (excludeId ?? 0), ct))
            slug = $"{baseSlug}-{suffix++}";
        return slug;
    }

    private static void UpsertCatTranslation(Category entity, string lang, string name, string? desc)
    {
        var t = entity.Translations.FirstOrDefault(x => x.LanguageCode == lang);
        if (t is null)
        {
            t = new CategoryTranslation { LanguageCode = lang };
            entity.Translations.Add(t);
        }
        t.Name = name.Trim();
        t.Description = desc;
    }

    private static CategoryTranslation? Pick(IEnumerable<CategoryTranslation> translations, string lang)
        => translations.FirstOrDefault(x => x.LanguageCode == lang);
}
