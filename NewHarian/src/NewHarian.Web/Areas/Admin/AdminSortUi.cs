using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using NewHarian.Application.Admin;

namespace NewHarian.Web.Areas.Admin;

public sealed class SortableThModel
{
    public required string Column { get; init; }
    public required string Label { get; init; }
    public required string CurrentSort { get; init; }
    public required string CurrentDir { get; init; }
}

public static class AdminSortUi
{
    /// <summary>
    /// Build list URL for a column header click: toggle dir if same column, else first-click default.
    /// Always resets page to 1; keeps other query params.
    /// </summary>
    public static string SortUrl(HttpRequest request, string column, string currentSort, string currentDir)
    {
        var dir = string.Equals(column, currentSort, StringComparison.OrdinalIgnoreCase)
            ? (AdminListQuery.IsAsc(currentDir) ? AdminListQuery.Desc : AdminListQuery.Asc)
            : AdminListQuery.DefaultDirForColumn(column);

        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in request.Query)
        {
            if (string.Equals(kv.Key, "page", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(kv.Key, "sort", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(kv.Key, "dir", StringComparison.OrdinalIgnoreCase)) continue;
            dict[kv.Key] = kv.Value.ToString();
        }

        dict["sort"] = column;
        dict["dir"] = dir;
        dict["page"] = "1";
        return QueryHelpers.AddQueryString(request.Path, dict!);
    }
}
