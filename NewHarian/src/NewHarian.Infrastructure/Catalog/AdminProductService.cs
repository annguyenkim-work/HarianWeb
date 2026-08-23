using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Catalog;

public class AdminProductService(
    AppDbContext db,
    IHtmlContentSanitizer html,
    ILogger<AdminProductService> logger) : IAdminProductService
{
    public async Task<IReadOnlyList<AdminProductListItemDto>> ListProductsAsync(
        int? categoryId,
        CancellationToken ct = default)
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

    public async Task<AdminProductEditDto?> GetProductAsync(int id, CancellationToken ct = default)
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
                v.ImageMediaFileId, v.Image?.StoredPath, v.StockQuantity)).ToList(),
            tagsCsv);
    }

    public async Task<(bool Ok, string? Error, int? Id)> SaveProductAsync(
        ProductSaveRequest request,
        CancellationToken ct = default)
    {
        request.Kind = CatalogKind.Product;
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
                    variant = entity.Variants.First(x => x.Id == vid);
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

    public async Task<(bool Ok, string? Error)> DeleteProductAsync(int id, CancellationToken ct = default)
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

    public async Task MoveProductAsync(int id, int direction, CancellationToken ct = default)
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

    public async Task<IReadOnlyList<VariantSuggestDto>> SuggestVariantsAsync(
        string? q,
        int take = 15,
        CancellationToken ct = default)
    {
        var term = (q ?? string.Empty).Trim();
        if (term.Length < 1)
            return Array.Empty<VariantSuggestDto>();

        take = Math.Clamp(take, 1, 30);
        var lower = term.ToLowerInvariant();
        var rows = await db.ProductVariants.AsNoTracking()
            .Where(v => v.IsActive && v.Product.Status != ProductStatus.Archived)
            .Where(v =>
                v.Sku.ToLower().Contains(lower) ||
                v.VariantLabel.ToLower().Contains(lower) ||
                v.Product.Translations.Any(t => t.Name.ToLower().Contains(lower)))
            .OrderBy(v => v.Sku)
            .Take(take)
            .Select(v => new
            {
                v.Id,
                v.Sku,
                v.VariantLabel,
                v.Price,
                Name = v.Product.Translations
                    .Where(t => t.LanguageCode == "vi")
                    .Select(t => t.Name)
                    .FirstOrDefault()
                    ?? v.Product.Translations.Select(t => t.Name).FirstOrDefault()
                    ?? v.Product.Slug
            })
            .ToListAsync(ct);

        return rows.Select(v => ToSuggestDto(v.Id, v.Sku, v.Name, v.VariantLabel, v.Price)).ToList();
    }

    public async Task<string?> GetVariantDisplayAsync(int variantId, CancellationToken ct = default)
    {
        var v = await db.ProductVariants.AsNoTracking()
            .Where(x => x.Id == variantId)
            .Select(x => new
            {
                x.Id,
                x.Sku,
                x.VariantLabel,
                x.Price,
                Name = x.Product.Translations
                    .Where(t => t.LanguageCode == "vi")
                    .Select(t => t.Name)
                    .FirstOrDefault()
                    ?? x.Product.Translations.Select(t => t.Name).FirstOrDefault()
                    ?? x.Product.Slug
            })
            .FirstOrDefaultAsync(ct);
        return v is null ? null : ToSuggestDto(v.Id, v.Sku, v.Name, v.VariantLabel, v.Price).Display;
    }

    private async Task<string> EnsureUniqueProductSlugAsync(
        int categoryId,
        string baseSlug,
        int? excludeId,
        CancellationToken ct)
    {
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Products.AnyAsync(
                   p => p.CategoryId == categoryId && p.Slug == slug && p.Id != (excludeId ?? 0), ct))
            slug = $"{baseSlug}-{suffix++}";
        return slug;
    }

    private static void UpsertProdTranslation(
        Product entity,
        string lang,
        string name,
        string? shortDesc,
        string? desc)
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

    private (bool Ok, string? Error, int? Id) RejectSaveProduct(string error)
    {
        logger.LogWarning("SaveProduct Done rejected Error={Error}", error);
        return (false, error, null);
    }

    private static ProductTranslation? PickP(IEnumerable<ProductTranslation> translations, string lang)
        => translations.FirstOrDefault(x => x.LanguageCode == lang);

    private static VariantSuggestDto ToSuggestDto(
        int id,
        string sku,
        string name,
        string variantLabel,
        decimal price)
    {
        var label = string.IsNullOrWhiteSpace(variantLabel) ? name : $"{name} - {variantLabel}";
        var display = $"{label} ({sku}) · {price:N0}đ";
        return new VariantSuggestDto(id, sku, name, variantLabel, price, display);
    }
}
