namespace NewHarian.Application.Admin;

/// <summary>Shared parse helpers for admin list ?sort=&amp;dir=&amp;from=&amp;to=.</summary>
public static class AdminListQuery
{
    public const string Asc = "asc";
    public const string Desc = "desc";

    public static string NormalizeDir(string? dir, string fallback = Desc)
    {
        if (string.Equals(dir, Asc, StringComparison.OrdinalIgnoreCase)) return Asc;
        if (string.Equals(dir, Desc, StringComparison.OrdinalIgnoreCase)) return Desc;
        return fallback is Asc ? Asc : Desc;
    }

    public static string NormalizeSort(string? sort, IReadOnlySet<string> whitelist, string defaultSort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return defaultSort;
        var key = sort.Trim();
        return whitelist.Contains(key) ? key : defaultSort;
    }

    /// <summary>First-click default: dates / money / id → desc; text / enum → asc.</summary>
    public static string DefaultDirForColumn(string sortKey) => sortKey switch
    {
        "createdAt" or "preferredDate" or "id" or "total" or "hasCv" => Desc,
        _ => Asc
    };

    /// <summary>Empty = open bound; swap when both set and from &gt; to.</summary>
    public static (DateOnly? From, DateOnly? To) NormalizeDateRange(DateOnly? from, DateOnly? to)
    {
        if (from is null || to is null) return (from, to);
        return from <= to ? (from, to) : (to, from);
    }

    public static bool IsAsc(string dir) =>
        string.Equals(dir, Asc, StringComparison.OrdinalIgnoreCase);

    /// <summary>Clamp page/pageSize and return Skip for SQL paging. pageSize max 100.</summary>
    public static (int Page, int PageSize, int Skip) PageBounds(int page, int pageSize, int totalCount, int defaultSize = 10)
    {
        if (pageSize < 1) pageSize = defaultSize;
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;
        var pages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > pages) page = pages;
        return (page, pageSize, (page - 1) * pageSize);
    }
}
