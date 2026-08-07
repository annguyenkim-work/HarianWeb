namespace NewHarian.Web.Areas.Admin;

public sealed class AdminPagerModel
{
    public const int DefaultPageSize = 10;

    public int Page { get; init; }
    public int PageSize { get; init; } = DefaultPageSize;
    public int TotalCount { get; init; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;
    public int FromItem => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int ToItem => Math.Min(Page * PageSize, TotalCount);
    /// <summary>0-based index of first item on this page within the full list.</summary>
    public int Offset => Math.Max(0, (Page - 1) * PageSize);
}

public static class AdminPaging
{
    public static (IReadOnlyList<T> Items, AdminPagerModel Pager) Apply<T>(
        IReadOnlyList<T> source,
        int page,
        int pageSize = AdminPagerModel.DefaultPageSize)
    {
        if (pageSize < 1) pageSize = AdminPagerModel.DefaultPageSize;
        if (page < 1) page = 1;

        var total = source.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var items = source.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var pager = new AdminPagerModel
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
        return (items, pager);
    }
}
