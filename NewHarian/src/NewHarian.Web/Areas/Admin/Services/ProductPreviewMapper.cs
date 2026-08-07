using NewHarian.Application.Catalog;

using NewHarian.Domain.Enums;

using NewHarian.Infrastructure.Catalog;



namespace NewHarian.Web.Areas.Admin.Services;



public sealed class ProductPreviewSnapshot

{

    public required ProductSaveRequest Request { get; init; }

    public required string CategorySlug { get; init; }

    public required IReadOnlyDictionary<string, string> CategoryNames { get; init; }

    public required IReadOnlyDictionary<int, IReadOnlyList<ColorTranslationSnapshot>> Colors { get; init; }

}



public sealed record ColorTranslationSnapshot(

    string LanguageCode,

    string Name,

    string? Meaning);



public static class ProductPreviewMapper

{

    public static ProductDetailDto ToDetail(ProductPreviewSnapshot snap, string lang)

    {

        lang = NormalizeLang(lang);

        var req = snap.Request;

        var hasSize = req.HasVariantSize;

        var hasColor = req.HasVariantColor;

        var name = Pick(lang, req.NameVi, req.NameEn, req.NameJa);

        var shortDesc = Pick(lang, req.ShortVi, req.ShortEn, req.ShortJa);

        var desc = Pick(lang, req.DescVi, req.DescEn, req.DescJa);

        var categoryName = snap.CategoryNames.TryGetValue(lang, out var cn)

            ? cn

            : snap.CategoryNames.GetValueOrDefault("vi") ?? snap.CategorySlug;



        var slug = string.IsNullOrWhiteSpace(req.Slug)

            ? Application.Abstractions.SlugHelper.FromVietnamese(req.NameVi)

            : req.Slug.Trim();

        if (string.IsNullOrWhiteSpace(slug)) slug = "preview";



        var mainUrl = string.IsNullOrWhiteSpace(req.MainImageUrl) ? null : req.MainImageUrl.Trim();



        var orderedVariants = (req.Variants ?? [])

            .Where(v => v.IsActive)

            .Where(v => !string.IsNullOrWhiteSpace(v.Sku) || v.Price > 0 || !string.IsNullOrWhiteSpace(v.VariantLabel) || v.ColorDefinitionId.HasValue)

            .OrderBy(v => v.SortOrder)

            .ToList();



        var slides = ProductGalleryHelper.BuildGallerySlides(

            mainUrl,

            orderedVariants.Select(v => string.IsNullOrWhiteSpace(v.ImageUrl) ? null : v.ImageUrl.Trim()));



        var variantDtos = orderedVariants.Select((v, i) =>

        {

            ColorTranslationSnapshot? colorTr = null;

            if (hasColor && v.ColorDefinitionId is int colorId

                && snap.Colors.TryGetValue(colorId, out var trs))

            {

                colorTr = trs.FirstOrDefault(t => t.LanguageCode == lang)

                          ?? trs.FirstOrDefault(t => t.LanguageCode == "vi")

                          ?? trs.FirstOrDefault();

            }



            var colorName = hasColor ? (colorTr?.Name ?? "").Trim() : "";
            var sizePart = hasSize ? (v.VariantLabel ?? "").Trim() : "";
            var label = string.Join(" / ", new[] { sizePart, colorName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(label))
                label = string.IsNullOrWhiteSpace(v.VariantLabel) ? (v.Sku ?? $"Option {i + 1}") : v.VariantLabel;

            var variantImageUrl = string.IsNullOrWhiteSpace(v.ImageUrl) ? null : v.ImageUrl.Trim();
            var slideIndex = ProductGalleryHelper.ResolveSlideIndex(slides, mainUrl, variantImageUrl);

            return new ProductVariantDto(
                -(i + 1),
                string.IsNullOrWhiteSpace(v.Sku) ? $"PREVIEW-{i + 1}" : v.Sku.Trim(),
                label,
                v.Price,
                v.IsDefault,
                hasColor ? colorTr?.Meaning : null,
                variantImageUrl,
                slideIndex,
                string.IsNullOrWhiteSpace(sizePart) ? null : sizePart,
                string.IsNullOrWhiteSpace(colorName) ? null : colorName);

        }).ToList();



        if (variantDtos.Count > 0 && variantDtos.Count(v => v.IsDefault) != 1)

        {

            variantDtos = variantDtos.Select((v, i) => v with { IsDefault = i == 0 }).ToList();

        }



        return new ProductDetailDto(

            req.Id ?? 0,

            snap.CategorySlug,

            categoryName,

            slug,

            string.IsNullOrWhiteSpace(name) ? "-" : name,

            string.IsNullOrWhiteSpace(shortDesc) ? null : shortDesc,

            string.IsNullOrWhiteSpace(desc) ? null : desc,

            req.Kind,

            req.HidePrice,

            variantDtos,

            slides,

            Array.Empty<ProductCardDto>());

    }



    private static string NormalizeLang(string lang)

        => lang is "en" or "ja" ? lang : "vi";



    private static string Pick(string lang, string? vi, string? en, string? ja)

    {

        var primary = lang switch

        {

            "en" => en,

            "ja" => ja,

            _ => vi

        };

        if (!string.IsNullOrWhiteSpace(primary)) return primary.Trim();

        if (!string.IsNullOrWhiteSpace(vi)) return vi.Trim();

        if (!string.IsNullOrWhiteSpace(en)) return en.Trim();

        return ja?.Trim() ?? "";

    }

}


