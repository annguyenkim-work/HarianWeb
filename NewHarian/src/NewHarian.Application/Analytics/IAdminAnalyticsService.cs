using NewHarian.Domain.Enums;

namespace NewHarian.Application.Analytics;

public sealed record AnalyticsDateRange(DateOnly Start, DateOnly End);

public sealed record AnalyticsDayPointDto(DateOnly Date, long Views);

public sealed record AnalyticsPageKindCountDto(PageKind PageKind, string Label, long Views);

public sealed record AnalyticsProductRankDto(int ProductId, string Name, string Slug, string CategorySlug, long Views);

public sealed class AdminAnalyticsDto
{
    public AnalyticsDateRange Range { get; init; } = new(default, default);
    public long TotalPageViews { get; init; }
    public long TotalProductDetailViews { get; init; }
    public IReadOnlyList<AnalyticsDayPointDto> PageViewsByDay { get; init; } = [];
    public IReadOnlyList<AnalyticsPageKindCountDto> ByPageKind { get; init; } = [];
    public IReadOnlyList<AnalyticsProductRankDto> TopProducts { get; init; } = [];
}

public interface IAdminAnalyticsService
{
    Task<AdminAnalyticsDto> GetAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
}
