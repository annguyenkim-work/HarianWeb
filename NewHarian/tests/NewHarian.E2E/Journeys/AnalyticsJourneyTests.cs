using NewHarian.Application.Abstractions;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P1")]
public sealed class AnalyticsJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Guest_views_increase_counts_on_analytics_page_for_sales_manager()
    {
        await using (var guest = await fx.NewGuestContextAsync())
        {
            await fx.RunAsync(guest, "Guest_opens_home_and_product_detail", async page =>
            {
                await page.GotoAsync("/");
                await Expect(page.Locator("body")).ToBeVisibleAsync();

                var catalog = new ProductCatalogPage(page);
                await catalog.GotoAsync();
                await catalog.OpenProductAsync(
                    E2ETestData.ProductName, E2ETestData.CategorySlug, E2ETestData.ProductSlug);
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = E2ETestData.ProductName, Level = 1 }))
                    .ToBeVisibleAsync();
                await Expect(page.Locator("#variant-price")).ToHaveTextAsync("150.000 VNĐ");
            });
        }

        await using var manager = await fx.NewRoleContextAsync(AppRoles.SalesManager);
        await fx.RunAsync(manager, nameof(Guest_views_increase_counts_on_analytics_page_for_sales_manager), async page =>
        {
            var shell = new AdminShell(page);
            await page.GotoAsync("/Admin");
            await Expect(shell.SidebarLink("Thống kê truy cập")).ToHaveCountAsync(1);
            await shell.SidebarLink("Thống kê truy cập").EvaluateAsync("a => a.click()");
            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Admin/Analytics", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Thống kê truy cập", Level = 1 })).ToBeVisibleAsync();

            var totalText = await page.Locator("[data-kpi=total-pageviews] .dash-kpi__value").InnerTextAsync();
            Assert.True(long.TryParse(totalText.Replace(".", "").Replace(",", ""), out var total) && total >= 2,
                $"Expected total pageviews >= 2, got '{totalText}'");

            var detailText = await page.Locator("[data-kpi=product-detail-views] .dash-kpi__value").InnerTextAsync();
            Assert.True(long.TryParse(detailText.Replace(".", "").Replace(",", ""), out var details) && details >= 1,
                $"Expected product detail views >= 1, got '{detailText}'");

            await Expect(page.Locator("[data-top-products-table]")).ToContainTextAsync(E2ETestData.ProductName);
        });
    }

    [E2EFact]
    public async Task Sales_staff_does_not_see_analytics_menu()
    {
        await using var staff = await fx.NewRoleContextAsync(AppRoles.SalesStaff);
        await fx.RunAsync(staff, nameof(Sales_staff_does_not_see_analytics_menu), async page =>
        {
            await page.GotoAsync("/Admin/Orders");
            var shell = new AdminShell(page);
            await Expect(shell.SidebarLink("Thống kê truy cập")).ToHaveCountAsync(0);
            await Expect(shell.SidebarLinkTo("/Admin/Analytics")).ToHaveCountAsync(0);
        });
    }
}
