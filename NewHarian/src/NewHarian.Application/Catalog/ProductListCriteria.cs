using System.Globalization;

namespace NewHarian.Application.Catalog;

public enum ProductListSort
{
    Recommended = 0,
    Name = 1,
    PriceAsc = 2,
    PriceDesc = 3
}

/// <summary>
/// Guest product listing query. Default sort is category order, then product order inside that category, then Id.
/// Page size is fixed at 12.
/// </summary>
public sealed record ProductListCriteria(
    int Page,
    ProductListSort Sort,
    string? CategorySlug,
    string? TagSlug,
    decimal? MinPrice,
    decimal? MaxPrice)
{
    public const int PageSize = 12;
    public const int MaxPage = 10_000;

    public int ActiveFilterCount =>
        (TagSlug is not null ? 1 : 0) + (MinPrice is not null || MaxPrice is not null ? 1 : 0);

    public static ProductListCriteria Normalize(
        int page,
        string? sort,
        string? categorySlug,
        string? tagSlug,
        decimal? minPrice,
        decimal? maxPrice)
    {
        var min = CleanPrice(minPrice);
        var max = CleanPrice(maxPrice);
        if (min is not null && max is not null && min > max)
            (min, max) = (max, min);

        var safePage = page < 1 ? 1 : Math.Min(page, MaxPage);
        return new ProductListCriteria(
            safePage,
            ParseSort(sort),
            string.IsNullOrWhiteSpace(categorySlug) ? null : categorySlug.Trim(),
            CleanTag(tagSlug),
            min,
            max);
    }

    public static ProductListSort ParseSort(string? sort) => sort?.Trim().ToLowerInvariant() switch
    {
        "name" => ProductListSort.Name,
        "price-asc" => ProductListSort.PriceAsc,
        "price-desc" => ProductListSort.PriceDesc,
        _ => ProductListSort.Recommended
    };

    public static string Key(ProductListSort sort) => sort switch
    {
        ProductListSort.Name => "name",
        ProductListSort.PriceAsc => "price-asc",
        ProductListSort.PriceDesc => "price-desc",
        _ => "recommended"
    };

    public string ForPage(int page) => Build(page < 1 ? 1 : page, Sort, TagSlug, MinPrice, MaxPrice);

    public string ForSort(ProductListSort sort) => Build(1, sort, TagSlug, MinPrice, MaxPrice);

    public string ForTag(string? tagSlug) => Build(1, Sort, tagSlug, MinPrice, MaxPrice);

    public string ForPrice(decimal? minPrice, decimal? maxPrice) => Build(1, Sort, TagSlug, minPrice, maxPrice);

    public string WithoutTagOrPrice() => Build(1, Sort, null, null, null);

    public static (int Start, int End) ResultRange(int page, int pageSize, int total)
    {
        if (total <= 0 || pageSize <= 0 || page < 1) return (0, 0);
        var start = (page - 1) * pageSize + 1;
        if (start > total) return (0, 0);
        return (start, Math.Min(page * pageSize, total));
    }

    /// <summary>Page numbers with null entries where an ellipsis belongs.</summary>
    public static IReadOnlyList<int?> PageWindow(int current, int totalPages, int siblingCount = 1)
    {
        if (totalPages <= 0) return Array.Empty<int?>();
        if (totalPages == 1) return new int?[] { 1 };

        var focus = current < 1 || current > totalPages ? 1 : current;
        var pages = new SortedSet<int> { 1, totalPages };
        var radius = Math.Max(0, siblingCount);
        for (var i = focus - radius; i <= focus + radius; i++)
        {
            if (i >= 1 && i <= totalPages) pages.Add(i);
        }

        var window = new List<int?>();
        int? previous = null;
        foreach (var page in pages)
        {
            if (previous is int prior && page - prior > 1)
                window.Add(null);
            window.Add(page);
            previous = page;
        }

        return window;
    }

    private static string Build(int page, ProductListSort sort, string? tag, decimal? min, decimal? max)
    {
        var parts = new List<string>(5);
        if (sort != ProductListSort.Recommended)
            parts.Add("sort=" + Key(sort));
        if (!string.IsNullOrEmpty(tag))
            parts.Add("tag=" + Uri.EscapeDataString(tag));
        if (min is decimal minPrice)
            parts.Add("min=" + minPrice.ToString("0", CultureInfo.InvariantCulture));
        if (max is decimal maxPrice)
            parts.Add("max=" + maxPrice.ToString("0", CultureInfo.InvariantCulture));
        if (page > 1)
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    private static decimal? CleanPrice(decimal? value)
    {
        if (value is not decimal amount || amount < 0) return null;
        return decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
    }

    private static string? CleanTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var trimmed = tag.Trim().ToLowerInvariant();
        if (trimmed.Length > 80) trimmed = trimmed[..80];
        foreach (var c in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
                return null;
        }

        return trimmed.Trim('-', '_').Length == 0 ? null : trimmed;
    }
}
