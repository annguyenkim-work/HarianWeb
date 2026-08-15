using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Catalog;

public class CatalogService(AppDbContext db) : ICatalogService
{
    public Task<IReadOnlyList<CategoryCardDto>> GetActiveCategoriesAsync(string lang, CancellationToken ct = default)
        => ProjectCategoryCardsAsync(
            db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder),
            lang, ct);

    public Task<IReadOnlyList<CategoryCardDto>> GetHomeCategoriesAsync(string lang, CancellationToken ct = default)
        => ProjectCategoryCardsAsync(
            db.Categories.AsNoTracking().Where(c => c.IsActive && c.ShowOnHome).OrderBy(c => c.SortOrder),
            lang, ct);

    public async Task<IReadOnlyList<ProductCardDto>> GetFeaturedProductsAsync(string lang, int take = 6, CancellationToken ct = default)
    {
        lang = NormalizeLang(lang);
        var products = await ProjectProductCards(
                db.Products.AsNoTracking()
                    .Where(p => p.Status == ProductStatus.Published && p.IsFeatured && p.Category.IsActive)
                    .OrderBy(p => p.SortOrder)
                    .Take(take),
                lang)
            .ToListAsync(ct);

        if (products.Count >= take)
            return products;

        var need = take - products.Count;
        var services = await ProjectServiceCards(
                db.Services.AsNoTracking()
                    .Where(s => s.Status == ProductStatus.Published && s.IsFeatured && s.Category.IsActive)
                    .OrderBy(s => s.SortOrder)
                    .Take(need),
                lang)
            .ToListAsync(ct);

        products.AddRange(services);
        return products;
    }

    public async Task<CategoryCardDto?> GetCategoryAsync(string categorySlug, string lang, CancellationToken ct = default)
    {
        var list = await ProjectCategoryCardsAsync(
            db.Categories.AsNoTracking().Where(c => c.IsActive && c.Slug == categorySlug),
            lang, ct);
        return list.FirstOrDefault();
    }

    public async Task<(IReadOnlyList<ProductCardDto> Items, int Total)> GetProductsByCategoryAsync(
        string categorySlug, string lang, int page, int pageSize, CatalogKind kind, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);
        lang = NormalizeLang(lang);

        if (kind == CatalogKind.Service)
        {
            var query = db.Services.AsNoTracking()
                .Where(s => s.Status == ProductStatus.Published && s.Category.Slug == categorySlug && s.Category.IsActive);
            var total = await query.CountAsync(ct);
            var items = await ProjectServiceCards(
                    query.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Skip((page - 1) * pageSize).Take(pageSize),
                    lang)
                .ToListAsync(ct);
            return (items, total);
        }
        else
        {
            var query = db.Products.AsNoTracking()
                .Where(p => p.Status == ProductStatus.Published && p.Category.Slug == categorySlug && p.Category.IsActive);
            var total = await query.CountAsync(ct);
            var items = await ProjectProductCards(
                    query.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize),
                    lang)
                .ToListAsync(ct);
            return (items, total);
        }
    }

    public async Task<(IReadOnlyList<ProductCardDto> Items, int Total)> GetProductsByTypeAsync(
        CatalogKind kind, string lang, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);
        lang = NormalizeLang(lang);

        if (kind == CatalogKind.Service)
        {
            var query = db.Services.AsNoTracking()
                .Where(s => s.Status == ProductStatus.Published && s.Category.IsActive);
            var total = await query.CountAsync(ct);
            var items = await ProjectServiceCards(
                    query.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Skip((page - 1) * pageSize).Take(pageSize),
                    lang)
                .ToListAsync(ct);
            return (items, total);
        }
        else
        {
            var query = db.Products.AsNoTracking()
                .Where(p => p.Status == ProductStatus.Published && p.Category.IsActive);
            var total = await query.CountAsync(ct);
            var items = await ProjectProductCards(
                    query.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize),
                    lang)
                .ToListAsync(ct);
            return (items, total);
        }
    }

    public async Task<(IReadOnlyList<ProductCardDto> Items, int Total)> SearchProductsAsync(
        string query, string lang, int page, int pageSize, string? tagSlug = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);
        lang = NormalizeLang(lang);
        var q = (query ?? "").Trim();
        var tag = (tagSlug ?? "").Trim().ToLowerInvariant();
        var hasQuery = q.Length > 0;
        var hasTag = tag.Length > 0;

        if (!hasQuery && !hasTag)
            return (Array.Empty<ProductCardDto>(), 0);

        var productQuery = db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published && p.Category.IsActive);

        if (hasTag)
            productQuery = productQuery.Where(p => p.ProductTags.Any(pt => pt.Tag.Slug == tag));

        if (hasQuery)
            productQuery = FilterProductsByTerm(productQuery, q);

        var productCards = ProjectProductCards(productQuery, lang);

        IQueryable<ProductCardDto> combined;
        if (hasTag)
        {
            combined = productCards;
        }
        else
        {
            var serviceQuery = db.Services.AsNoTracking()
                .Where(s => s.Status == ProductStatus.Published && s.Category.IsActive);
            if (hasQuery)
                serviceQuery = FilterServicesByTerm(serviceQuery, q);
            combined = productCards.Concat(ProjectServiceCards(serviceQuery, lang));
        }

        var total = await combined.CountAsync(ct);
        var items = await combined
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
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

        var related = await ProjectProductCards(
                db.Products.AsNoTracking()
                    .Where(x => x.Status == ProductStatus.Published && x.CategoryId == p.CategoryId && x.Id != p.Id)
                    .OrderBy(x => x.SortOrder)
                    .Take(4),
                NormalizeLang(lang))
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
            related);
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

        var related = await ProjectServiceCards(
                db.Services.AsNoTracking()
                    .Where(x => x.Status == ProductStatus.Published && x.CategoryId == s.CategoryId && x.Id != s.Id)
                    .OrderBy(x => x.SortOrder)
                    .Take(4),
                NormalizeLang(lang))
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
            related);
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

    private async Task<IReadOnlyList<CategoryCardDto>> ProjectCategoryCardsAsync(
        IQueryable<Domain.Entities.Category> query, string lang, CancellationToken ct)
    {
        lang = NormalizeLang(lang);
        var published = ProductStatus.Published;
        return await query
            .Select(c => new CategoryCardDto(
                c.Id,
                c.Slug,
                c.Translations.Where(t => t.LanguageCode == lang).Select(t => t.Name).FirstOrDefault()
                    ?? c.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.Name).FirstOrDefault()
                    ?? c.Slug,
                c.ImageUrl,
                c.Products.Count(p => p.Status == published) + c.Services.Count(s => s.Status == published),
                c.Products.Count(p => p.Status == published),
                c.Services.Count(s => s.Status == published)))
            .ToListAsync(ct);
    }

    private static IQueryable<ProductCardDto> ProjectProductCards(IQueryable<Domain.Entities.Product> query, string lang)
        => query.Select(p => new ProductCardDto(
            p.Id,
            p.Category.Slug,
            p.Slug,
            p.Translations.Where(t => t.LanguageCode == lang).Select(t => t.Name).FirstOrDefault()
                ?? p.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.Name).FirstOrDefault()
                ?? p.Slug,
            CatalogKind.Product,
            p.Variants.Where(v => v.IsActive).OrderByDescending(v => v.IsDefault).ThenBy(v => v.SortOrder)
                .Select(v => (decimal?)v.Price).FirstOrDefault(),
            p.MainImage != null ? p.MainImage.StoredPath : null,
            false,
            p.Translations.Where(t => t.LanguageCode == lang).Select(t => t.ShortDescription).FirstOrDefault()
                ?? p.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.ShortDescription).FirstOrDefault()));

    private static IQueryable<ProductCardDto> ProjectServiceCards(IQueryable<Domain.Entities.Service> query, string lang)
        => query.Select(s => new ProductCardDto(
            s.Id,
            s.Category.Slug,
            s.Slug,
            s.Translations.Where(t => t.LanguageCode == lang).Select(t => t.Name).FirstOrDefault()
                ?? s.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.Name).FirstOrDefault()
                ?? s.Slug,
            CatalogKind.Service,
            s.HidePrice
                ? null
                : s.Variants.Where(v => v.IsActive).OrderByDescending(v => v.IsDefault).ThenBy(v => v.SortOrder)
                    .Select(v => (decimal?)v.Price).FirstOrDefault(),
            s.MainImage != null ? s.MainImage.StoredPath : null,
            s.HidePrice,
            s.Translations.Where(t => t.LanguageCode == lang).Select(t => t.ShortDescription).FirstOrDefault()
                ?? s.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.ShortDescription).FirstOrDefault()));

    private IQueryable<Domain.Entities.Product> FilterProductsByTerm(IQueryable<Domain.Entities.Product> query, string term)
    {
        if (UseILike)
        {
            var pattern = ToILikePattern(term);
            return query.Where(p =>
                p.Translations.Any(t =>
                    EF.Functions.ILike(t.Name, pattern) ||
                    (t.ShortDescription != null && EF.Functions.ILike(t.ShortDescription, pattern))) ||
                p.Variants.Any(v => v.IsActive && (
                    EF.Functions.ILike(v.Sku, pattern) ||
                    EF.Functions.ILike(v.VariantLabel, pattern))) ||
                p.ProductTags.Any(pt =>
                    EF.Functions.ILike(pt.Tag.Name, pattern) ||
                    EF.Functions.ILike(pt.Tag.Slug, pattern)));
        }

        var lower = term.ToLowerInvariant();
        return query.Where(p =>
            p.Translations.Any(t =>
                t.Name.ToLower().Contains(lower) ||
                (t.ShortDescription != null && t.ShortDescription.ToLower().Contains(lower))) ||
            p.Variants.Any(v => v.IsActive && (
                v.Sku.ToLower().Contains(lower) ||
                v.VariantLabel.ToLower().Contains(lower))) ||
            p.ProductTags.Any(pt =>
                pt.Tag.Name.ToLower().Contains(lower) ||
                pt.Tag.Slug.ToLower().Contains(lower)));
    }

    private IQueryable<Domain.Entities.Service> FilterServicesByTerm(IQueryable<Domain.Entities.Service> query, string term)
    {
        if (UseILike)
        {
            var pattern = ToILikePattern(term);
            return query.Where(s =>
                s.Translations.Any(t =>
                    EF.Functions.ILike(t.Name, pattern) ||
                    (t.ShortDescription != null && EF.Functions.ILike(t.ShortDescription, pattern))) ||
                s.Variants.Any(v => v.IsActive && (
                    EF.Functions.ILike(v.Sku, pattern) ||
                    EF.Functions.ILike(v.VariantLabel, pattern))));
        }

        var lower = term.ToLowerInvariant();
        return query.Where(s =>
            s.Translations.Any(t =>
                t.Name.ToLower().Contains(lower) ||
                (t.ShortDescription != null && t.ShortDescription.ToLower().Contains(lower))) ||
            s.Variants.Any(v => v.IsActive && (
                v.Sku.ToLower().Contains(lower) ||
                v.VariantLabel.ToLower().Contains(lower))));
    }

    private bool UseILike =>
        db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private static string ToILikePattern(string term)
    {
        var escaped = term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return "%" + escaped + "%";
    }

    private static string NormalizeLang(string lang) => lang is "en" or "ja" ? lang : "vi";

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
