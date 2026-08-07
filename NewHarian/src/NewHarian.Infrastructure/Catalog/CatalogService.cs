using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Catalog;

public class CatalogService(AppDbContext db) : ICatalogService
{
    public async Task<IReadOnlyList<CategoryCardDto>> GetActiveCategoriesAsync(string lang, CancellationToken ct = default)
    {
        var cats = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Include(c => c.Translations)
            .Include(c => c.Products.Where(p => p.Status == ProductStatus.Published))
            .Include(c => c.Services.Where(s => s.Status == ProductStatus.Published))
            .ToListAsync(ct);

        return cats.Select(c => ToCategoryCard(c, lang)).ToList();
    }

    public async Task<IReadOnlyList<CategoryCardDto>> GetHomeCategoriesAsync(string lang, CancellationToken ct = default)
    {
        var cats = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.ShowOnHome)
            .OrderBy(c => c.SortOrder)
            .Include(c => c.Translations)
            .Include(c => c.Products.Where(p => p.Status == ProductStatus.Published))
            .Include(c => c.Services.Where(s => s.Status == ProductStatus.Published))
            .ToListAsync(ct);

        return cats.Select(c => ToCategoryCard(c, lang)).ToList();
    }

    public async Task<IReadOnlyList<ProductCardDto>> GetFeaturedProductsAsync(string lang, int take = 6, CancellationToken ct = default)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published && p.IsFeatured && p.Category.IsActive)
            .OrderBy(p => p.SortOrder)
            .Take(take)
            .Include(p => p.Translations)
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.MainImage)
            .Include(p => p.Category)
            .ToListAsync(ct);

        var services = await db.Services.AsNoTracking()
            .Where(s => s.Status == ProductStatus.Published && s.IsFeatured && s.Category.IsActive)
            .OrderBy(s => s.SortOrder)
            .Take(take)
            .Include(s => s.Translations)
            .Include(s => s.Variants.Where(v => v.IsActive))
            .Include(s => s.MainImage)
            .Include(s => s.Category)
            .ToListAsync(ct);

        var items = products.Select(p => ToCard(p, lang)).ToList();
        if (items.Count < take)
            items.AddRange(services.Select(s => ToCard(s, lang)).Take(take - items.Count));
        return items.Take(take).ToList();
    }

    public async Task<CategoryCardDto?> GetCategoryAsync(string categorySlug, string lang, CancellationToken ct = default)
    {
        var c = await db.Categories
            .AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.Products.Where(p => p.Status == ProductStatus.Published))
            .Include(x => x.Services.Where(s => s.Status == ProductStatus.Published))
            .FirstOrDefaultAsync(x => x.IsActive && x.Slug == categorySlug, ct);
        if (c is null) return null;
        return ToCategoryCard(c, lang);
    }

    public async Task<(IReadOnlyList<ProductCardDto> Items, int Total)> GetProductsByCategoryAsync(
        string categorySlug, string lang, int page, int pageSize, CatalogKind kind, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);

        if (kind == CatalogKind.Service)
        {
            var query = db.Services.AsNoTracking()
                .Where(s => s.Status == ProductStatus.Published && s.Category.Slug == categorySlug && s.Category.IsActive);
            var total = await query.CountAsync(ct);
            var services = await query
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(s => s.Translations)
                .Include(s => s.Variants.Where(v => v.IsActive))
                .Include(s => s.MainImage)
                .Include(s => s.Category)
                .ToListAsync(ct);
            return (services.Select(s => ToCard(s, lang)).ToList(), total);
        }
        else
        {
            var query = db.Products.AsNoTracking()
                .Where(p => p.Status == ProductStatus.Published && p.Category.Slug == categorySlug && p.Category.IsActive);
            var total = await query.CountAsync(ct);
            var products = await query
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(p => p.Translations)
                .Include(p => p.Variants.Where(v => v.IsActive))
                .Include(p => p.MainImage)
                .Include(p => p.Category)
                .ToListAsync(ct);
            return (products.Select(p => ToCard(p, lang)).ToList(), total);
        }
    }

    public async Task<(IReadOnlyList<ProductCardDto> Items, int Total)> GetProductsByTypeAsync(
        CatalogKind kind, string lang, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);

        if (kind == CatalogKind.Service)
        {
            var query = db.Services.AsNoTracking()
                .Where(s => s.Status == ProductStatus.Published && s.Category.IsActive);
            var total = await query.CountAsync(ct);
            var services = await query
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(s => s.Translations)
                .Include(s => s.Variants.Where(v => v.IsActive))
                .Include(s => s.MainImage)
                .Include(s => s.Category)
                .ToListAsync(ct);
            return (services.Select(s => ToCard(s, lang)).ToList(), total);
        }
        else
        {
            var query = db.Products.AsNoTracking()
                .Where(p => p.Status == ProductStatus.Published && p.Category.IsActive);
            var total = await query.CountAsync(ct);
            var products = await query
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(p => p.Translations)
                .Include(p => p.Variants.Where(v => v.IsActive))
                .Include(p => p.MainImage)
                .Include(p => p.Category)
                .ToListAsync(ct);
            return (products.Select(p => ToCard(p, lang)).ToList(), total);
        }
    }

    public async Task<(IReadOnlyList<ProductCardDto> Items, int Total)> SearchProductsAsync(
        string query, string lang, int page, int pageSize, string? tagSlug = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);
        var q = (query ?? "").Trim();
        var tag = (tagSlug ?? "").Trim().ToLowerInvariant();
        var term = q.ToLowerInvariant();
        var hasQuery = term.Length > 0;
        var hasTag = tag.Length > 0;

        if (!hasQuery && !hasTag)
            return (Array.Empty<ProductCardDto>(), 0);

        var productQuery = db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published && p.Category.IsActive);

        if (hasTag)
            productQuery = productQuery.Where(p => p.ProductTags.Any(pt => pt.Tag.Slug == tag));

        if (hasQuery)
        {
            productQuery = productQuery.Where(p =>
                p.Translations.Any(t =>
                    t.Name.ToLower().Contains(term) ||
                    (t.ShortDescription != null && t.ShortDescription.ToLower().Contains(term))) ||
                p.Variants.Any(v => v.IsActive && (
                    v.Sku.ToLower().Contains(term) ||
                    v.VariantLabel.ToLower().Contains(term))) ||
                p.ProductTags.Any(pt =>
                    pt.Tag.Name.ToLower().Contains(term) ||
                    pt.Tag.Slug.ToLower().Contains(term)));
        }

        var products = await productQuery
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Include(p => p.Translations)
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.MainImage)
            .Include(p => p.Category)
            .ToListAsync(ct);

        IReadOnlyList<ProductCardDto> serviceCards = Array.Empty<ProductCardDto>();
        if (!hasTag)
        {
            var serviceQuery = db.Services.AsNoTracking()
                .Where(s => s.Status == ProductStatus.Published && s.Category.IsActive);

            if (hasQuery)
            {
                serviceQuery = serviceQuery.Where(s =>
                    s.Translations.Any(t =>
                        t.Name.ToLower().Contains(term) ||
                        (t.ShortDescription != null && t.ShortDescription.ToLower().Contains(term))) ||
                    s.Variants.Any(v => v.IsActive && (
                        v.Sku.ToLower().Contains(term) ||
                        v.VariantLabel.ToLower().Contains(term))));
            }

            var services = await serviceQuery
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Include(s => s.Translations)
                .Include(s => s.Variants.Where(v => v.IsActive))
                .Include(s => s.MainImage)
                .Include(s => s.Category)
                .ToListAsync(ct);
            serviceCards = services.Select(s => ToCard(s, lang)).ToList();
        }

        var all = products.Select(p => ToCard(p, lang))
            .Concat(serviceCards)
            .OrderBy(c => c.Name)
            .ToList();

        var total = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public async Task<IReadOnlyList<TagChipDto>> GetPublishedProductTagsAsync(CancellationToken ct = default)
    {
        return await db.Tags.AsNoTracking()
            .Where(t => t.ProductTags.Any(pt =>
                pt.Product.Status == ProductStatus.Published && pt.Product.Category.IsActive))
            .OrderBy(t => t.Name)
            .Select(t => new TagChipDto(t.Slug, t.Name))
            .ToListAsync(ct);
    }

    public async Task<ProductDetailDto?> GetProductAsync(string categorySlug, string productSlug, string lang, CancellationToken ct = default)
    {
        var p = await db.Products.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.MainImage)
            .Include(x => x.Variants.Where(v => v.IsActive))
                .ThenInclude(v => v.ColorDefinition)
                    .ThenInclude(c => c!.Translations)
            .Include(x => x.Variants.Where(v => v.IsActive))
                .ThenInclude(v => v.Image)
            .Include(x => x.Category).ThenInclude(c => c.Translations)
            .FirstOrDefaultAsync(x =>
                x.Status == ProductStatus.Published &&
                x.Slug == productSlug &&
                x.Category.Slug == categorySlug &&
                x.Category.IsActive, ct);

        if (p is null) return null;

        var related = await db.Products.AsNoTracking()
            .Where(x => x.Status == ProductStatus.Published && x.CategoryId == p.CategoryId && x.Id != p.Id)
            .OrderBy(x => x.SortOrder)
            .Take(4)
            .Include(x => x.Translations)
            .Include(x => x.Variants.Where(v => v.IsActive))
            .Include(x => x.MainImage)
            .Include(x => x.Category)
            .ToListAsync(ct);

        var t = PickTranslation(p.Translations, lang);
        var mainPath = p.MainImage?.StoredPath;
        var orderedVariants = p.Variants.OrderBy(v => v.SortOrder).ToList();
        var slides = ProductGalleryHelper.BuildGallerySlides(
            mainPath,
            orderedVariants.Select(v => v.Image?.StoredPath));

        var variantDtos = orderedVariants.Select(v => MapVariant(v, p.HasVariantSize, p.HasVariantColor, lang, mainPath, slides)).ToList();

        return new ProductDetailDto(
            p.Id,
            p.Category.Slug,
            PickName(p.Category.Translations, lang),
            p.Slug,
            t?.Name ?? p.Slug,
            t?.ShortDescription,
            t?.Description,
            CatalogKind.Product,
            false,
            variantDtos,
            slides,
            related.Select(x => ToCard(x, lang)).ToList());
    }

    public async Task<ProductDetailDto?> GetServiceAsync(string categorySlug, string productSlug, string lang, CancellationToken ct = default)
    {
        var s = await db.Services.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.MainImage)
            .Include(x => x.Variants.Where(v => v.IsActive))
                .ThenInclude(v => v.ColorDefinition)
                    .ThenInclude(c => c!.Translations)
            .Include(x => x.Variants.Where(v => v.IsActive))
                .ThenInclude(v => v.Image)
            .Include(x => x.Category).ThenInclude(c => c.Translations)
            .FirstOrDefaultAsync(x =>
                x.Status == ProductStatus.Published &&
                x.Slug == productSlug &&
                x.Category.Slug == categorySlug &&
                x.Category.IsActive, ct);

        if (s is null) return null;

        var related = await db.Services.AsNoTracking()
            .Where(x => x.Status == ProductStatus.Published && x.CategoryId == s.CategoryId && x.Id != s.Id)
            .OrderBy(x => x.SortOrder)
            .Take(4)
            .Include(x => x.Translations)
            .Include(x => x.Variants.Where(v => v.IsActive))
            .Include(x => x.MainImage)
            .Include(x => x.Category)
            .ToListAsync(ct);

        var t = PickTranslation(s.Translations, lang);
        var mainPath = s.MainImage?.StoredPath;
        var orderedVariants = s.Variants.OrderBy(v => v.SortOrder).ToList();
        var slides = ProductGalleryHelper.BuildGallerySlides(
            mainPath,
            orderedVariants.Select(v => v.Image?.StoredPath));

        var variantDtos = orderedVariants.Select(v => MapVariant(v, s.HasVariantSize, s.HasVariantColor, lang, mainPath, slides)).ToList();

        return new ProductDetailDto(
            s.Id,
            s.Category.Slug,
            PickName(s.Category.Translations, lang),
            s.Slug,
            t?.Name ?? s.Slug,
            t?.ShortDescription,
            t?.Description,
            CatalogKind.Service,
            s.HidePrice,
            variantDtos,
            slides,
            related.Select(x => ToCard(x, lang)).ToList());
    }

    public async Task<ProductVariantDto?> GetVariantAsync(int variantId, string lang, CatalogKind kind, CancellationToken ct = default)
    {
        if (kind == CatalogKind.Service)
        {
            var sv = await db.ServiceVariants.AsNoTracking()
                .Include(x => x.Image)
                .Include(x => x.Service).ThenInclude(s => s.MainImage)
                .Include(x => x.ColorDefinition).ThenInclude(c => c!.Translations)
                .FirstOrDefaultAsync(x => x.Id == variantId && x.IsActive, ct);
            if (sv is null) return null;

            var service = sv.Service;
            var svMainPath = service.MainImage?.StoredPath;

            var svSiblingImagePaths = await db.ServiceVariants.AsNoTracking()
                .Where(vv => vv.ServiceId == service.Id && vv.IsActive)
                .OrderBy(vv => vv.SortOrder)
                .Select(vv => vv.Image != null ? vv.Image.StoredPath : null)
                .ToListAsync(ct);

            var svSlides = ProductGalleryHelper.BuildGallerySlides(svMainPath, svSiblingImagePaths);
            return MapVariant(sv, service.HasVariantSize, service.HasVariantColor, lang, svMainPath, svSlides);
        }

        var v = await db.ProductVariants.AsNoTracking()
            .Include(x => x.Image)
            .Include(x => x.Product).ThenInclude(p => p.MainImage)
            .Include(x => x.ColorDefinition).ThenInclude(c => c!.Translations)
            .FirstOrDefaultAsync(x => x.Id == variantId && x.IsActive, ct);
        if (v is null) return null;

        var product = v.Product;
        var mainPath = product.MainImage?.StoredPath;

        // Load sibling variant images separately — Include Product→Variants would cycle under AsNoTracking.
        var siblingImagePaths = await db.ProductVariants.AsNoTracking()
            .Where(vv => vv.ProductId == product.Id && vv.IsActive)
            .OrderBy(vv => vv.SortOrder)
            .Select(vv => vv.Image != null ? vv.Image.StoredPath : null)
            .ToListAsync(ct);

        var slides = ProductGalleryHelper.BuildGallerySlides(mainPath, siblingImagePaths);
        return MapVariant(v, product.HasVariantSize, product.HasVariantColor, lang, mainPath, slides);
    }

    private static ProductVariantDto MapVariant(
        Domain.Entities.ProductVariant v,
        bool hasSize,
        bool hasColor,
        string lang,
        string? mainPath,
        List<string> slides)
    {
        var sizePart = hasSize ? (v.VariantLabel ?? "").Trim() : "";
        var colorTr = (hasColor && v.ColorDefinition is not null)
            ? PickColorTranslation(v.ColorDefinition.Translations, lang)
            : null;
        var colorName = hasColor ? (colorTr?.Name ?? "").Trim() : "";

        var label = string.Join(" / ", new[] { sizePart, colorName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(label)) label = (v.VariantLabel ?? "").Trim();

        var variantImagePath = v.Image?.StoredPath;
        var slideIndex = ProductGalleryHelper.ResolveSlideIndex(slides, mainPath, variantImagePath);

        return new ProductVariantDto(
            v.Id,
            v.Sku,
            label,
            v.Price,
            v.IsDefault,
            hasColor ? colorTr?.Meaning : null,
            variantImagePath,
            slideIndex,
            string.IsNullOrWhiteSpace(sizePart) ? null : sizePart,
            string.IsNullOrWhiteSpace(colorName) ? null : colorName);
    }

    private static ProductVariantDto MapVariant(
        Domain.Entities.ServiceVariant v,
        bool hasSize,
        bool hasColor,
        string lang,
        string? mainPath,
        List<string> slides)
    {
        var sizePart = hasSize ? (v.VariantLabel ?? "").Trim() : "";
        var colorTr = (hasColor && v.ColorDefinition is not null)
            ? PickColorTranslation(v.ColorDefinition.Translations, lang)
            : null;
        var colorName = hasColor ? (colorTr?.Name ?? "").Trim() : "";

        var label = string.Join(" / ", new[] { sizePart, colorName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(label)) label = (v.VariantLabel ?? "").Trim();

        var variantImagePath = v.Image?.StoredPath;
        var slideIndex = ProductGalleryHelper.ResolveSlideIndex(slides, mainPath, variantImagePath);

        return new ProductVariantDto(
            v.Id,
            v.Sku,
            label,
            v.Price,
            v.IsDefault,
            hasColor ? colorTr?.Meaning : null,
            variantImagePath,
            slideIndex,
            string.IsNullOrWhiteSpace(sizePart) ? null : sizePart,
            string.IsNullOrWhiteSpace(colorName) ? null : colorName);
    }

    private static CategoryCardDto ToCategoryCard(Domain.Entities.Category c, string lang)
    {
        var physical = c.Products.Count;
        var service = c.Services.Count;
        return new CategoryCardDto(
            c.Id,
            c.Slug,
            PickName(c.Translations, lang),
            c.ImageUrl,
            physical + service,
            physical,
            service);
    }

    private static ProductCardDto ToCard(Domain.Entities.Product p, string lang)
    {
        var t = PickTranslation(p.Translations, lang);
        decimal? price = p.Variants.OrderByDescending(v => v.IsDefault).ThenBy(v => v.SortOrder).Select(v => (decimal?)v.Price).FirstOrDefault();
        var img = p.MainImage?.StoredPath;
        return new ProductCardDto(
            p.Id,
            p.Category.Slug,
            p.Slug,
            t?.Name ?? p.Slug,
            CatalogKind.Product,
            price,
            img,
            false,
            string.IsNullOrWhiteSpace(t?.ShortDescription) ? null : t.ShortDescription.Trim());
    }

    private static ProductCardDto ToCard(Domain.Entities.Service s, string lang)
    {
        var t = PickTranslation(s.Translations, lang);
        decimal? price = s.HidePrice
            ? null
            : s.Variants.OrderByDescending(v => v.IsDefault).ThenBy(v => v.SortOrder).Select(v => (decimal?)v.Price).FirstOrDefault();
        var img = s.MainImage?.StoredPath;
        return new ProductCardDto(
            s.Id,
            s.Category.Slug,
            s.Slug,
            t?.Name ?? s.Slug,
            CatalogKind.Service,
            price,
            img,
            s.HidePrice,
            string.IsNullOrWhiteSpace(t?.ShortDescription) ? null : t.ShortDescription.Trim());
    }

    private static string PickName(IEnumerable<Domain.Entities.CategoryTranslation> translations, string lang)
        => PickTranslation(translations, lang)?.Name ?? translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? "";

    private static Domain.Entities.CategoryTranslation? PickTranslation(IEnumerable<Domain.Entities.CategoryTranslation> translations, string lang)
        => translations.FirstOrDefault(t => t.LanguageCode == lang)
           ?? translations.FirstOrDefault(t => t.LanguageCode == "vi")
           ?? translations.FirstOrDefault();

    private static Domain.Entities.ProductTranslation? PickTranslation(IEnumerable<Domain.Entities.ProductTranslation> translations, string lang)
        => translations.FirstOrDefault(t => t.LanguageCode == lang)
           ?? translations.FirstOrDefault(t => t.LanguageCode == "vi")
           ?? translations.FirstOrDefault();

    private static Domain.Entities.ServiceTranslation? PickTranslation(IEnumerable<Domain.Entities.ServiceTranslation> translations, string lang)
        => translations.FirstOrDefault(t => t.LanguageCode == lang)
           ?? translations.FirstOrDefault(t => t.LanguageCode == "vi")
           ?? translations.FirstOrDefault();

    private static Domain.Entities.ColorDefinitionTranslation? PickColorTranslation(IEnumerable<Domain.Entities.ColorDefinitionTranslation> translations, string lang)
        => translations.FirstOrDefault(t => t.LanguageCode == lang)
           ?? translations.FirstOrDefault(t => t.LanguageCode == "vi")
           ?? translations.FirstOrDefault();
}
