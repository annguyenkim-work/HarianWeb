using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Analytics;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Analytics;

public sealed class AdminAnalyticsService(AppDbContext db) : IAdminAnalyticsService
{
    private const int TopProductLimit = 50;

    public async Task<AdminAnalyticsDto> GetAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var range = AnalyticsTime.NormalizeRange(start, end);

        var pageRows = await db.PageViewDaily.AsNoTracking()
            .Where(x => x.Date >= range.Start && x.Date <= range.End)
            .ToListAsync(ct);

        var totalPageViews = pageRows.Sum(x => x.ViewCount);
        var byDayRaw = pageRows.GroupBy(x => x.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.ViewCount));
        var pageViewsByDay = new List<AnalyticsDayPointDto>();
        for (var d = range.Start; d <= range.End; d = d.AddDays(1))
            pageViewsByDay.Add(new AnalyticsDayPointDto(d, byDayRaw.GetValueOrDefault(d)));

        var byKind = pageRows
            .GroupBy(x => x.PageKind)
            .Select(g => new AnalyticsPageKindCountDto(g.Key, PageViewClassifier.LabelVi(g.Key), g.Sum(x => x.ViewCount)))
            .OrderByDescending(x => x.Views)
            .ToList();

        var productAgg = await db.ProductViewDaily.AsNoTracking()
            .Where(x => x.Date >= range.Start && x.Date <= range.End)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Views = g.Sum(x => x.ViewCount) })
            .OrderByDescending(x => x.Views)
            .Take(TopProductLimit)
            .ToListAsync(ct);

        var totalProductDetailViews = pageRows.Where(x => x.PageKind == PageKind.ProductDetail).Sum(x => x.ViewCount);
        var productIds = productAgg.Select(x => x.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Slug,
                CategorySlug = p.Category.Slug,
                Name = p.Translations.Where(t => t.LanguageCode == "vi").Select(t => t.Name).FirstOrDefault()
                       ?? p.Translations.Select(t => t.Name).FirstOrDefault()
                       ?? p.Slug
            })
            .ToListAsync(ct);
        var nameById = products.ToDictionary(p => p.Id);

        var topProducts = productAgg
            .Select(a =>
            {
                nameById.TryGetValue(a.ProductId, out var p);
                return new AnalyticsProductRankDto(
                    a.ProductId,
                    p?.Name ?? $"#{a.ProductId}",
                    p?.Slug ?? "",
                    p?.CategorySlug ?? "",
                    a.Views);
            })
            .ToList();

        return new AdminAnalyticsDto
        {
            Range = range,
            TotalPageViews = totalPageViews,
            TotalProductDetailViews = totalProductDetailViews,
            PageViewsByDay = pageViewsByDay,
            ByPageKind = byKind,
            TopProducts = topProducts,
        };
    }
}
