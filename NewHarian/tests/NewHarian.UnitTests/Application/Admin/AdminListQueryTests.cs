using NewHarian.Application.Admin;

namespace NewHarian.UnitTests.Application.Admin;

[Trait("Category", "Unit")]
public class AdminListQueryTests
{
    private static readonly IReadOnlySet<string> Whitelist =
        new HashSet<string>(StringComparer.Ordinal) { "createdAt", "name", "total" };

    [Theory]
    [InlineData("asc", AdminListQuery.Desc, "asc")]
    [InlineData("ASC", AdminListQuery.Desc, "asc")]
    [InlineData("Desc", AdminListQuery.Asc, "desc")]
    [InlineData(null, AdminListQuery.Desc, "desc")]
    [InlineData(null, AdminListQuery.Asc, "asc")]
    [InlineData("sideways", AdminListQuery.Asc, "asc")]
    [InlineData("sideways", "garbage", "desc")]
    public void NormalizeDir_accepts_asc_desc_or_falls_back(string? dir, string fallback, string expected)
    {
        Assert.Equal(expected, AdminListQuery.NormalizeDir(dir, fallback));
    }

    [Theory]
    [InlineData(null, "createdAt")]
    [InlineData("   ", "createdAt")]
    [InlineData("name", "name")]
    [InlineData("  total ", "total")]
    [InlineData("Name", "createdAt")]
    [InlineData("drop table", "createdAt")]
    public void NormalizeSort_returns_whitelisted_key_or_default(string? sort, string expected)
    {
        Assert.Equal(expected, AdminListQuery.NormalizeSort(sort, Whitelist, "createdAt"));
    }

    [Theory]
    [InlineData("createdAt", "desc")]
    [InlineData("preferredDate", "desc")]
    [InlineData("id", "desc")]
    [InlineData("total", "desc")]
    [InlineData("hasCv", "desc")]
    [InlineData("name", "asc")]
    [InlineData("status", "asc")]
    public void DefaultDirForColumn_sorts_dates_and_money_descending(string column, string expected)
    {
        Assert.Equal(expected, AdminListQuery.DefaultDirForColumn(column));
    }

    [Fact]
    public void NormalizeDateRange_swaps_reversed_bounds()
    {
        var (from, to) = AdminListQuery.NormalizeDateRange(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 1));

        Assert.Equal(new DateOnly(2026, 10, 1), from);
        Assert.Equal(new DateOnly(2026, 10, 6), to);
    }

    [Fact]
    public void NormalizeDateRange_keeps_open_bounds()
    {
        DateOnly? day = new DateOnly(2026, 10, 6);
        DateOnly? open = null;

        Assert.Equal((day, open), AdminListQuery.NormalizeDateRange(day, open));
        Assert.Equal((open, day), AdminListQuery.NormalizeDateRange(open, day));
        Assert.Equal((day, day), AdminListQuery.NormalizeDateRange(day, day));
    }

    [Theory]
    [InlineData("asc", true)]
    [InlineData("ASC", true)]
    [InlineData("desc", false)]
    public void IsAsc_is_case_insensitive(string dir, bool expected)
    {
        Assert.Equal(expected, AdminListQuery.IsAsc(dir));
    }

    [Theory]
    [InlineData(1, 10, 0, 10, 1, 10, 0)]
    [InlineData(0, 0, 50, 10, 1, 10, 0)]
    [InlineData(3, 10, 25, 10, 3, 10, 20)]
    [InlineData(9, 10, 25, 10, 3, 10, 20)]
    [InlineData(2, 500, 1000, 10, 2, 100, 100)]
    [InlineData(-1, -5, 5, 20, 1, 20, 0)]
    public void PageBounds_clamps_page_and_size(
        int page, int pageSize, int total, int defaultSize,
        int expectedPage, int expectedSize, int expectedSkip)
    {
        Assert.Equal((expectedPage, expectedSize, expectedSkip), AdminListQuery.PageBounds(page, pageSize, total, defaultSize));
    }
}
