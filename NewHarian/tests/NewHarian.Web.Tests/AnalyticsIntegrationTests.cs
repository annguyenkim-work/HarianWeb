using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Analytics;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Analytics;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Tests;

/// <summary>Guest pageview rollups + Admin Analytics page. Own fixture (login rate limit).</summary>
public class AnalyticsIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public AnalyticsIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    private async Task<(string CategorySlug, string ProductSlug, int ProductId)> SeedProductAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var product = new Product
        {
            Category = new Category { Slug = $"an-cat-{suffix}", IsActive = true },
            Slug = $"an-prod-{suffix}",
            Status = ProductStatus.Published,
            Translations = { new ProductTranslation { LanguageCode = "vi", Name = $"SP Analytics {suffix}" } },
            Variants = { new ProductVariant { Sku = $"AN-{suffix}", Price = 10_000m, IsActive = true, IsDefault = true } }
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product.Category.Slug, product.Slug, product.Id);
    }

    private async Task<long> PageViewsAsync(PageKind kind)
    {
        var today = AnalyticsTime.TodayLocal();
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PageViewDaily.AsNoTracking()
            .Where(x => x.Date == today && x.PageKind == kind)
            .Select(x => x.ViewCount)
            .FirstOrDefaultAsync();
    }

    private async Task<long> ProductViewsAsync(int productId)
    {
        var today = AnalyticsTime.TodayLocal();
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductViewDaily.AsNoTracking()
            .Where(x => x.Date == today && x.ProductId == productId)
            .Select(x => x.ViewCount)
            .FirstOrDefaultAsync();
    }

    [Fact]
    public async Task Guest_home_and_product_detail_increment_daily_rollups()
    {
        var (cat, slug, productId) = await SeedProductAsync();
        var guest = _factory.CreateClient();
        var beforeHome = await PageViewsAsync(PageKind.Home);
        var beforeDetail = await PageViewsAsync(PageKind.ProductDetail);
        var beforeProduct = await ProductViewsAsync(productId);

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/products/{cat}/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/products/{cat}/{slug}")).StatusCode);

        Assert.Equal(beforeHome + 1, await PageViewsAsync(PageKind.Home));
        Assert.Equal(beforeDetail + 2, await PageViewsAsync(PageKind.ProductDetail));
        Assert.Equal(beforeProduct + 2, await ProductViewsAsync(productId));
    }

    [Fact]
    public async Task Admin_json_api_and_404_do_not_count()
    {
        var before = await PageViewsAsync(PageKind.OtherPublic);
        var guest = _factory.CreateClient();

        using (var json = new HttpRequestMessage(HttpMethod.Get, "/cart"))
        {
            json.Headers.Add("Accept", "application/json");
            await guest.SendAsync(json);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/products/khong/co")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/health")).StatusCode);

        Assert.Equal(before, await PageViewsAsync(PageKind.OtherPublic));
    }

    [Fact]
    public async Task Sales_manager_sees_analytics_page_and_staff_is_forbidden()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IPageViewRecorder>();
            await recorder.RecordAsync(PageKind.Home);
            await recorder.RecordAsync(PageKind.Home);
        }

        var manager = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var html = WebUtility.HtmlDecode(await manager.GetStringAsync("/admin/Analytics"));
        Assert.Contains("Thống kê truy cập", html);
        Assert.Contains("data-kpi=\"total-pageviews\"", html);
        Assert.Contains("Top sản phẩm theo lượt xem", html);
        Assert.Contains("data-page-kind-table", html);
        var nav = WebUtility.HtmlDecode(await manager.GetStringAsync("/admin/Orders"));
        Assert.Contains("/Admin/Analytics", nav, StringComparison.OrdinalIgnoreCase);

        var staff = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff, allowRedirect: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Analytics");
        request.Headers.Add("Sec-Fetch-Mode", "cors");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.SendAsync(request)).StatusCode);

        var staffNav = WebUtility.HtmlDecode(await (await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff)).GetStringAsync("/admin/Orders"));
        Assert.DoesNotContain("/Admin/Analytics", staffNav, StringComparison.OrdinalIgnoreCase);
    }
}
