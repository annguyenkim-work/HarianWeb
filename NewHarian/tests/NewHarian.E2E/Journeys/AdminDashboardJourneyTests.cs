using NewHarian.Application.Abstractions;
using NewHarian.E2E.Fixtures;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P1")]
public sealed class AdminDashboardJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Super_admin_sees_commerce_kpis_and_trend_chart()
    {
        await using var context = await fx.NewRoleContextAsync(AppRoles.SuperAdmin);
        await fx.RunAsync(context, nameof(Super_admin_sees_commerce_kpis_and_trend_chart), async page =>
        {
            await page.GotoAsync("/admin");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Level = 1 })).ToBeVisibleAsync();
            await Expect(page.Locator("[data-commerce-kpis]")).ToBeVisibleAsync();
            await Expect(page.Locator("[data-kpi=revenue]")).ToBeVisibleAsync();
            await Expect(page.Locator("[data-kpi=orders]")).ToBeVisibleAsync();
            await Expect(page.Locator("[data-kpi=aov]")).ToBeVisibleAsync();
            await Expect(page.Locator("#chart-orders-trend")).ToBeVisibleAsync();
            await Expect(page.Locator("[data-ops-strip]")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "7 ngày" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "30 ngày" })).ToBeVisibleAsync();
        });
    }

    [E2EFact]
    public async Task Hr_staff_sees_ops_strip_without_commerce_revenue()
    {
        await using var context = await fx.NewRoleContextAsync(AppRoles.HrStaff);
        await fx.RunAsync(context, nameof(Hr_staff_sees_ops_strip_without_commerce_revenue), async page =>
        {
            await page.GotoAsync("/admin");
            await Expect(page.Locator("[data-ops-strip]")).ToBeVisibleAsync();
            await Expect(page.GetByText("Hồ sơ ứng tuyển mới")).ToBeVisibleAsync();
            await Expect(page.Locator("[data-commerce-kpis]")).ToHaveCountAsync(0);
            await Expect(page.Locator("#chart-orders-trend")).ToHaveCountAsync(0);
        });
    }
}
