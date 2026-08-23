using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Catalog;

public class AdminServiceOfferingService(
    AppDbContext db,
    IHtmlContentSanitizer html,
    ILogger<AdminServiceOfferingService> logger) : IAdminServiceOfferingService
{
    public async Task<IReadOnlyList<AdminProductListItemDto>> ListServicesAsync(
        int? categoryId,
        CancellationToken ct = default)
    {
        var q = db.Services.AsNoTracking()
            .Include(s => s.Translations)
            .Include(s => s.Variants)
            .Include(s => s.Category)
            .AsQueryable();
        if (categoryId.HasValue) q = q.Where(s => s.CategoryId == categoryId);

        var list = await q.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync(ct);
        return list.Select(s => new AdminProductListItemDto(
            s.Id,
            s.CategoryId,
            s.Category.Slug,
            s.Slug,
            s.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? s.Slug,
            CatalogKind.Service,
            s.Status,
            s.Variants.Count,
            s.HidePrice
                ? null
                : s.Variants.Where(v => v.IsActive).Select(v => (decimal?)v.Price).DefaultIfEmpty().Min())).ToList();
    }

    public async Task<AdminProductEditDto?> GetServiceAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Services.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.Variants).ThenInclude(v => v.Image)
            .Include(x => x.MainImage)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;
        return new AdminProductEditDto(
            s.Id, s.CategoryId, s.Slug, CatalogKind.Service, s.Status, s.IsFeatured, s.SortOrder,
            s.HasVariantSize, s.HasVariantColor, s.HidePrice, s.MainImageMediaFileId, s.MainImage?.StoredPath,
            PickS(s.Translations, "vi")?.Name ?? "", PickS(s.Translations, "vi")?.ShortDescription, PickS(s.Translations, "vi")?.Description,
            PickS(s.Translations, "en")?.Name ?? "", PickS(s.Translations, "en")?.ShortDescription, PickS(s.Translations, "en")?.Description,
            PickS(s.Translations, "ja")?.Name ?? "", PickS(s.Translations, "ja")?.ShortDescription, PickS(s.Translations, "ja")?.Description,
            s.Variants.OrderBy(v => v.SortOrder).Select(v => new AdminVariantEditDto(
                v.Id, v.Sku, v.VariantLabel, v.ColorDefinitionId, v.Price, v.IsDefault, v.SortOrder, v.IsActive,
                v.ImageMediaFileId, v.Image?.StoredPath)).ToList());
    }

    public async Task<(bool Ok, string? Error, int? Id)> SaveServiceAsync(
        ProductSaveRequest request,
        CancellationToken ct = default)
    {
        request.Kind = CatalogKind.Service;
        logger.LogInformation("SaveService Start Id={Id} CategoryId={CategoryId}", request.Id, request.CategoryId);
        try
        {
            if (string.IsNullOrWhiteSpace(request.NameVi))
                return RejectSaveService("Tên tiếng Việt bắt buộc.");
            var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
            if (category is null)
                return RejectSaveService("Danh mục không hợp lệ.");

            var hasSize = request.HasVariantSize;
            var hasColor = request.HasVariantColor;
            var variants = request.Variants
                .Where(v => !string.IsNullOrWhiteSpace(v.Sku))
                .Where(v =>
                    (!hasSize || !string.IsNullOrWhiteSpace(v.VariantLabel)) &&
                    (!hasColor || v.ColorDefinitionId.HasValue))
                .ToList();
            if (request.Status == ProductStatus.Published && variants.Count == 0)
                return RejectSaveService("Published cần ≥ 1 variant.");
            if (variants.Count > 0 && variants.Count(v => v.IsDefault) != 1)
            {
                variants[0].IsDefault = true;
                for (var i = 1; i < variants.Count; i++) variants[i].IsDefault = false;
            }

            var mediaIds = new List<int>();
            if (request.MainImageMediaFileId is int mainId and > 0)
                mediaIds.Add(mainId);
            foreach (var v in variants)
            {
                if (v.ImageMediaFileId is int vid and > 0)
                    mediaIds.Add(vid);
            }
            mediaIds = mediaIds.Distinct().ToList();
            if (mediaIds.Count > 0)
            {
                var existingMedia = await db.MediaFiles.CountAsync(m => mediaIds.Contains(m.Id), ct);
                if (existingMedia != mediaIds.Count)
                    return RejectSaveService("Một số ảnh không hợp lệ.");
            }

            var skus = variants.Select(v => v.Sku.Trim()).ToList();
            if (skus.Distinct(StringComparer.OrdinalIgnoreCase).Count() != skus.Count)
                return RejectSaveService("SKU trùng trong form.");

            var otherSku = await db.ServiceVariants
                .Where(v => skus.Contains(v.Sku) && v.ServiceId != (request.Id ?? 0))
                .Select(v => v.Sku)
                .FirstOrDefaultAsync(ct);
            if (otherSku is not null) return RejectSaveService($"SKU '{otherSku}' đã dùng.");

            Service entity;
            if (request.Id is int id)
            {
                entity = await db.Services
                    .Include(s => s.Translations)
                    .Include(s => s.Variants)
                    .FirstOrDefaultAsync(s => s.Id == id, ct)
                    ?? throw new InvalidOperationException("Service not found");
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var baseSlug = SlugHelper.FromVietnamese(request.NameVi);
                if (string.IsNullOrWhiteSpace(baseSlug))
                    return RejectSaveService("Không tạo được slug từ tên tiếng Việt.");

                var slug = await EnsureUniqueServiceSlugAsync(request.CategoryId, baseSlug, null, ct);
                var maxOrder = await db.Services
                    .Where(s => s.CategoryId == request.CategoryId)
                    .MaxAsync(s => (int?)s.SortOrder, ct) ?? 0;
                entity = new Service
                {
                    CreatedAt = DateTime.UtcNow,
                    Slug = slug,
                    SortOrder = maxOrder + 1
                };
                db.Services.Add(entity);
            }

            entity.CategoryId = request.CategoryId;
            entity.Status = request.Status;
            entity.IsFeatured = request.IsFeatured;
            entity.HasVariantSize = hasSize;
            entity.HasVariantColor = hasColor;
            entity.HidePrice = request.HidePrice;
            entity.MainImageMediaFileId = request.MainImageMediaFileId is > 0 ? request.MainImageMediaFileId : null;

            UpsertServiceTranslation(entity, "vi", request.NameVi, request.ShortVi, html.Sanitize(request.DescVi));
            UpsertServiceTranslation(entity, "en", string.IsNullOrWhiteSpace(request.NameEn) ? request.NameVi : request.NameEn, request.ShortEn, html.Sanitize(request.DescEn));
            UpsertServiceTranslation(entity, "ja", string.IsNullOrWhiteSpace(request.NameJa) ? request.NameVi : request.NameJa, request.ShortJa, html.Sanitize(request.DescJa));

            var keepIds = variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
            foreach (var existing in entity.Variants.Where(v => !keepIds.Contains(v.Id)).ToList())
                db.ServiceVariants.Remove(existing);

            var sort = 0;
            foreach (var v in variants)
            {
                ServiceVariant variant;
                if (v.Id is int vid)
                    variant = entity.Variants.First(x => x.Id == vid);
                else
                {
                    variant = new ServiceVariant();
                    entity.Variants.Add(variant);
                }
                variant.Sku = v.Sku.Trim();
                variant.VariantLabel = hasSize ? (v.VariantLabel?.Trim() ?? "") : "";
                variant.ColorDefinitionId = hasColor ? v.ColorDefinitionId : null;
                variant.ImageMediaFileId = v.ImageMediaFileId is > 0 ? v.ImageMediaFileId : null;
                variant.Price = v.Price;
                variant.IsDefault = v.IsDefault;
                variant.SortOrder = v.SortOrder != 0 ? v.SortOrder : sort;
                variant.IsActive = v.IsActive;
                sort++;
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveService Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveService Error Id={Id}", request.Id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteServiceAsync(int id, CancellationToken ct = default)
    {
        logger.LogInformation("DeleteService Start Id={Id}", id);
        try
        {
            var s = await db.Services.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (s is null)
            {
                logger.LogWarning("DeleteService Done rejected Id={Id} Error={Error}", id, "Không tìm thấy.");
                return (false, "Không tìm thấy.");
            }
            if (s.Status == ProductStatus.Archived)
            {
                logger.LogInformation("DeleteService Done Id={Id} already archived", id);
                return (true, null);
            }

            s.Status = ProductStatus.Archived;
            s.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("DeleteService Done Id={Id} soft-archived", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteService Error Id={Id}", id);
            throw;
        }
    }

    public async Task MoveServiceAsync(int id, int direction, CancellationToken ct = default)
    {
        logger.LogInformation("MoveService Start Id={Id} Direction={Direction}", id, direction);
        try
        {
            var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (service is null)
            {
                logger.LogInformation("MoveService Done Id={Id} skipped", id);
                return;
            }

            var items = await db.Services
                .Where(s => s.CategoryId == service.CategoryId)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Id)
                .ToListAsync(ct);
            var idx = items.FindIndex(s => s.Id == id);
            var swapIdx = idx + direction;
            if (idx < 0 || swapIdx < 0 || swapIdx >= items.Count)
            {
                logger.LogInformation("MoveService Done Id={Id} skipped", id);
                return;
            }

            (items[idx].SortOrder, items[swapIdx].SortOrder) = (items[swapIdx].SortOrder, items[idx].SortOrder);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("MoveService Done Id={Id}", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveService Error Id={Id}", id);
            throw;
        }
    }

    private async Task<string> EnsureUniqueServiceSlugAsync(
        int categoryId,
        string baseSlug,
        int? excludeId,
        CancellationToken ct)
    {
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Services.AnyAsync(
                   s => s.CategoryId == categoryId && s.Slug == slug && s.Id != (excludeId ?? 0), ct))
            slug = $"{baseSlug}-{suffix++}";
        return slug;
    }

    private static void UpsertServiceTranslation(
        Service entity,
        string lang,
        string name,
        string? shortDesc,
        string? desc)
    {
        var t = entity.Translations.FirstOrDefault(x => x.LanguageCode == lang);
        if (t is null)
        {
            t = new ServiceTranslation { LanguageCode = lang };
            entity.Translations.Add(t);
        }
        t.Name = name.Trim();
        t.ShortDescription = shortDesc;
        t.Description = desc;
    }

    private (bool Ok, string? Error, int? Id) RejectSaveService(string error)
    {
        logger.LogWarning("SaveService Done rejected Error={Error}", error);
        return (false, error, null);
    }

    private static ServiceTranslation? PickS(IEnumerable<ServiceTranslation> translations, string lang)
        => translations.FirstOrDefault(x => x.LanguageCode == lang);
}
