using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Catalog;

public class AdminCatalogService(AppDbContext db, IHtmlContentSanitizer html, ILogger<AdminCatalogService> logger) : IAdminCatalogService
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

    public async Task<(bool Ok, string? Error, int? Id)> SaveCategoryAsync(CategorySaveRequest request, CancellationToken ct = default)
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
                entity = await db.Categories.Include(c => c.Translations).FirstOrDefaultAsync(c => c.Id == id, ct)
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

    private (bool Ok, string? Error, int? Id) RejectSaveCategory(string error)
    {
        logger.LogWarning("SaveCategory Done rejected Error={Error}", error);
        return (false, error, null);
    }

    private async Task<string> EnsureUniqueCategorySlugAsync(string baseSlug, int? excludeId, CancellationToken ct)
    {
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != (excludeId ?? 0), ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }
        return slug;
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

    public Task<IReadOnlyList<AdminProductListItemDto>> ListProductsAsync(int? categoryId, CatalogKind kind, CancellationToken ct = default)
        => kind == CatalogKind.Service ? ListServicesAsync(categoryId, ct) : ListPhysicalProductsAsync(categoryId, ct);

    private async Task<IReadOnlyList<AdminProductListItemDto>> ListPhysicalProductsAsync(int? categoryId, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking()
            .Include(p => p.Translations)
            .Include(p => p.Variants)
            .Include(p => p.Category)
            .AsQueryable();
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);

        var list = await q.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);
        return list.Select(p => new AdminProductListItemDto(
            p.Id,
            p.CategoryId,
            p.Category.Slug,
            p.Slug,
            p.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? p.Slug,
            CatalogKind.Product,
            p.Status,
            p.Variants.Count,
            p.Variants.Where(v => v.IsActive).Select(v => (decimal?)v.Price).DefaultIfEmpty().Min())).ToList();
    }

    private async Task<IReadOnlyList<AdminProductListItemDto>> ListServicesAsync(int? categoryId, CancellationToken ct)
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

    public Task<AdminProductEditDto?> GetProductAsync(int id, CatalogKind kind, CancellationToken ct = default)
        => kind == CatalogKind.Service ? GetServiceEditAsync(id, ct) : GetPhysicalProductEditAsync(id, ct);

    private async Task<AdminProductEditDto?> GetPhysicalProductEditAsync(int id, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.Variants).ThenInclude(v => v.Image)
            .Include(x => x.MainImage)
            .Include(x => x.ProductTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var tagsCsv = string.Join(", ", p.ProductTags.Select(pt => pt.Tag.Name).OrderBy(n => n));
        return new AdminProductEditDto(
            p.Id, p.CategoryId, p.Slug, CatalogKind.Product, p.Status, p.IsFeatured, p.SortOrder,
            p.HasVariantSize, p.HasVariantColor, false, p.MainImageMediaFileId, p.MainImage?.StoredPath,
            PickP(p.Translations, "vi")?.Name ?? "", PickP(p.Translations, "vi")?.ShortDescription, PickP(p.Translations, "vi")?.Description,
            PickP(p.Translations, "en")?.Name ?? "", PickP(p.Translations, "en")?.ShortDescription, PickP(p.Translations, "en")?.Description,
            PickP(p.Translations, "ja")?.Name ?? "", PickP(p.Translations, "ja")?.ShortDescription, PickP(p.Translations, "ja")?.Description,
            p.Variants.OrderBy(v => v.SortOrder).Select(v => new AdminVariantEditDto(
                v.Id, v.Sku, v.VariantLabel, v.ColorDefinitionId, v.Price, v.IsDefault, v.SortOrder, v.IsActive,
                v.ImageMediaFileId, v.Image?.StoredPath)).ToList(),
            tagsCsv);
    }

    private async Task<AdminProductEditDto?> GetServiceEditAsync(int id, CancellationToken ct)
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

    public Task<(bool Ok, string? Error, int? Id)> SaveProductAsync(ProductSaveRequest request, CancellationToken ct = default)
        => request.Kind == CatalogKind.Service ? SaveServiceAsync(request, ct) : SavePhysicalProductAsync(request, ct);

    private async Task<(bool Ok, string? Error, int? Id)> SavePhysicalProductAsync(ProductSaveRequest request, CancellationToken ct)
    {
        logger.LogInformation("SaveProduct Start Id={Id} CategoryId={CategoryId}", request.Id, request.CategoryId);
        try
        {
            if (string.IsNullOrWhiteSpace(request.NameVi))
                return RejectSaveProduct("Tên tiếng Việt bắt buộc.");
            var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
            if (category is null)
                return RejectSaveProduct("Danh mục không hợp lệ.");

            var hasSize = request.HasVariantSize;
            var hasColor = request.HasVariantColor;

            var variants = request.Variants
                .Where(v => !string.IsNullOrWhiteSpace(v.Sku))
                .Where(v =>
                    (!hasSize || !string.IsNullOrWhiteSpace(v.VariantLabel)) &&
                    (!hasColor || v.ColorDefinitionId.HasValue))
                .ToList();
            if (request.Status == ProductStatus.Published && variants.Count == 0)
                return RejectSaveProduct("Published cần ≥ 1 variant.");
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
                    return RejectSaveProduct("Một số ảnh không hợp lệ.");
            }

            var skus = variants.Select(v => v.Sku.Trim()).ToList();
            if (skus.Distinct(StringComparer.OrdinalIgnoreCase).Count() != skus.Count)
                return RejectSaveProduct("SKU trùng trong form.");

            var otherSku = await db.ProductVariants
                .Where(v => skus.Contains(v.Sku) && v.ProductId != (request.Id ?? 0))
                .Select(v => v.Sku)
                .FirstOrDefaultAsync(ct);
            if (otherSku is not null) return RejectSaveProduct($"SKU '{otherSku}' đã dùng.");

            Product entity;
            if (request.Id is int id)
            {
                entity = await db.Products
                    .Include(p => p.Translations)
                    .Include(p => p.Variants)
                    .Include(p => p.ProductTags)
                    .FirstOrDefaultAsync(p => p.Id == id, ct)
                    ?? throw new InvalidOperationException("Product not found");
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var baseSlug = SlugHelper.FromVietnamese(request.NameVi);
                if (string.IsNullOrWhiteSpace(baseSlug))
                    return RejectSaveProduct("Không tạo được slug từ tên tiếng Việt.");

                var slug = await EnsureUniqueProductSlugAsync(request.CategoryId, baseSlug, null, ct);
                var maxOrder = await db.Products
                    .Where(p => p.CategoryId == request.CategoryId)
                    .MaxAsync(p => (int?)p.SortOrder, ct) ?? 0;

                entity = new Product
                {
                    CreatedAt = DateTime.UtcNow,
                    Slug = slug,
                    SortOrder = maxOrder + 1
                };
                db.Products.Add(entity);
            }

            entity.CategoryId = request.CategoryId;
            entity.Status = request.Status;
            entity.IsFeatured = request.IsFeatured;
            entity.HasVariantSize = hasSize;
            entity.HasVariantColor = hasColor;
            entity.MainImageMediaFileId = request.MainImageMediaFileId is > 0 ? request.MainImageMediaFileId : null;

            UpsertProdTranslation(entity, "vi", request.NameVi, request.ShortVi, html.Sanitize(request.DescVi));
            UpsertProdTranslation(entity, "en", string.IsNullOrWhiteSpace(request.NameEn) ? request.NameVi : request.NameEn, request.ShortEn, html.Sanitize(request.DescEn));
            UpsertProdTranslation(entity, "ja", string.IsNullOrWhiteSpace(request.NameJa) ? request.NameVi : request.NameJa, request.ShortJa, html.Sanitize(request.DescJa));

            var keepIds = variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
            foreach (var existing in entity.Variants.Where(v => !keepIds.Contains(v.Id)).ToList())
                db.ProductVariants.Remove(existing);

            var sort = 0;
            foreach (var v in variants)
            {
                ProductVariant variant;
                if (v.Id is int vid)
                {
                    variant = entity.Variants.First(x => x.Id == vid);
                }
                else
                {
                    variant = new ProductVariant();
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
            await SyncProductTagsAsync(entity.Id, request.TagsCsv, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveProduct Done Id={Id}", entity.Id);
            return (true, null, entity.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveProduct Error Id={Id}", request.Id);
            throw;
        }
    }

    private async Task<(bool Ok, string? Error, int? Id)> SaveServiceAsync(ProductSaveRequest request, CancellationToken ct)
    {
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
                {
                    variant = entity.Variants.First(x => x.Id == vid);
                }
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

    private (bool Ok, string? Error, int? Id) RejectSaveProduct(string error)
    {
        logger.LogWarning("SaveProduct Done rejected Error={Error}", error);
        return (false, error, null);
    }

    private (bool Ok, string? Error, int? Id) RejectSaveService(string error)
    {
        logger.LogWarning("SaveService Done rejected Error={Error}", error);
        return (false, error, null);
    }

    public Task<(bool Ok, string? Error)> DeleteProductAsync(int id, CatalogKind kind, CancellationToken ct = default)
        => kind == CatalogKind.Service ? DeleteServiceAsync(id, ct) : DeletePhysicalProductAsync(id, ct);

    private async Task<(bool Ok, string? Error)> DeletePhysicalProductAsync(int id, CancellationToken ct)
    {
        logger.LogInformation("DeleteProduct Start Id={Id}", id);
        try
        {
            var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null)
            {
                logger.LogWarning("DeleteProduct Done rejected Id={Id} Error={Error}", id, "Không tìm thấy.");
                return (false, "Không tìm thấy.");
            }

            if (p.Status == ProductStatus.Archived)
            {
                logger.LogInformation("DeleteProduct Done Id={Id} already archived", id);
                return (true, null);
            }

            p.Status = ProductStatus.Archived;
            p.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("DeleteProduct Done Id={Id} soft-archived", id);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteProduct Error Id={Id}", id);
            throw;
        }
    }

    private async Task<(bool Ok, string? Error)> DeleteServiceAsync(int id, CancellationToken ct)
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

    public Task MoveProductAsync(int id, int direction, CatalogKind kind, CancellationToken ct = default)
        => kind == CatalogKind.Service ? MoveServiceAsync(id, direction, ct) : MovePhysicalProductAsync(id, direction, ct);

    private async Task MovePhysicalProductAsync(int id, int direction, CancellationToken ct)
    {
        logger.LogInformation("MoveProduct Start Id={Id} Direction={Direction}", id, direction);
        try
        {
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                logger.LogInformation("MoveProduct Done Id={Id} skipped", id);
                return;
            }

            var items = await db.Products
                .Where(p => p.CategoryId == product.CategoryId)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .ToListAsync(ct);
            var idx = items.FindIndex(p => p.Id == id);
            var swapIdx = idx + direction;
            if (idx < 0 || swapIdx < 0 || swapIdx >= items.Count)
            {
                logger.LogInformation("MoveProduct Done Id={Id} skipped", id);
                return;
            }

            (items[idx].SortOrder, items[swapIdx].SortOrder) = (items[swapIdx].SortOrder, items[idx].SortOrder);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("MoveProduct Done Id={Id}", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MoveProduct Error Id={Id}", id);
            throw;
        }
    }

    private async Task MoveServiceAsync(int id, int direction, CancellationToken ct)
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

    private async Task<string> EnsureUniqueProductSlugAsync(int categoryId, string baseSlug, int? excludeId, CancellationToken ct)
    {
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Products.AnyAsync(p => p.CategoryId == categoryId && p.Slug == slug && p.Id != (excludeId ?? 0), ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }
        return slug;
    }

    private async Task<string> EnsureUniqueServiceSlugAsync(int categoryId, string baseSlug, int? excludeId, CancellationToken ct)
    {
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Services.AnyAsync(s => s.CategoryId == categoryId && s.Slug == slug && s.Id != (excludeId ?? 0), ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }
        return slug;
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

    public async Task<IReadOnlyList<AdminColorDefinitionOptionDto>> GetColorDefinitionsAsync(CancellationToken ct = default)
    {
        var colors = await db.ColorDefinitions.AsNoTracking()
            .Include(c => c.Translations)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        return colors.Select(c => new AdminColorDefinitionOptionDto(
                c.Id,
                c.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
                ?? c.Translations.FirstOrDefault()?.Name
                ?? $"Color #{c.Id}"))
            .ToList();
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

    private static void UpsertProdTranslation(Product entity, string lang, string name, string? shortDesc, string? desc)
    {
        var t = entity.Translations.FirstOrDefault(x => x.LanguageCode == lang);
        if (t is null)
        {
            t = new ProductTranslation { LanguageCode = lang };
            entity.Translations.Add(t);
        }
        t.Name = name.Trim();
        t.ShortDescription = shortDesc;
        t.Description = desc;
    }

    private static void UpsertServiceTranslation(Service entity, string lang, string name, string? shortDesc, string? desc)
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

    private async Task SyncProductTagsAsync(int productId, string? tagsCsv, CancellationToken ct)
    {
        var names = (tagsCsv ?? "")
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        var desired = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var slug = SlugHelper.FromVietnamese(name);
            if (string.IsNullOrWhiteSpace(slug)) continue;
            if (!desired.ContainsKey(slug))
                desired[slug] = name.Length > 80 ? name[..80] : name;
        }

        var slugs = desired.Keys.ToList();
        var tags = await db.Tags.Where(t => slugs.Contains(t.Slug)).ToListAsync(ct);
        foreach (var (slug, name) in desired)
        {
            if (tags.Any(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase)))
                continue;
            var tag = new Tag { Slug = slug, Name = name };
            db.Tags.Add(tag);
            tags.Add(tag);
        }
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);

        var wantedIds = tags
            .Where(t => desired.ContainsKey(t.Slug))
            .Select(t => t.Id)
            .ToHashSet();

        var current = await db.ProductTags.Where(pt => pt.ProductId == productId).ToListAsync(ct);
        foreach (var pt in current.Where(pt => !wantedIds.Contains(pt.TagId)))
            db.ProductTags.Remove(pt);
        var have = current.Select(pt => pt.TagId).ToHashSet();
        foreach (var tagId in wantedIds.Where(id => !have.Contains(id)))
            db.ProductTags.Add(new ProductTag { ProductId = productId, TagId = tagId });
    }

    private static CategoryTranslation? Pick(IEnumerable<CategoryTranslation> t, string lang)
        => t.FirstOrDefault(x => x.LanguageCode == lang);

    private static ProductTranslation? PickP(IEnumerable<ProductTranslation> t, string lang)
        => t.FirstOrDefault(x => x.LanguageCode == lang);

    private static ServiceTranslation? PickS(IEnumerable<ServiceTranslation> t, string lang)
        => t.FirstOrDefault(x => x.LanguageCode == lang);
}
