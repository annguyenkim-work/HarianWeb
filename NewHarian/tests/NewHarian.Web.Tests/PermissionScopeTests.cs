using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;

namespace NewHarian.Web.Tests;

/// <summary>Notifications, dashboard and Help filtered per role. Own fixture (login rate limit, own notifications).</summary>
public class PermissionScopeTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public PermissionScopeTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Notifications_are_filtered_by_permission()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
            var notifications = scope.ServiceProvider.GetRequiredService<IAdminNotificationService>();
            await notifications.PublishAsync(AdminNotificationTypes.OrderCreated, "Đơn mới", null, "/admin/Orders", "Order", "1");
            await notifications.PublishAsync(AdminNotificationTypes.ApplicationCreated, "Hồ sơ mới", null, "/admin/Applications", "JobApplication", "1");
        }

        var hr = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrStaff);
        var hrTypes = await ListTypesAsync(hr);
        Assert.Contains(AdminNotificationTypes.ApplicationCreated, hrTypes);
        Assert.DoesNotContain(AdminNotificationTypes.OrderCreated, hrTypes);
        Assert.Equal(1, await UnreadCountAsync(hr));

        var warehouse = await TestUsers.LoggedInClientAsync(_factory, AppRoles.WarehouseStaff);
        var whTypes = await ListTypesAsync(warehouse);
        Assert.Contains(AdminNotificationTypes.OrderCreated, whTypes);
        Assert.DoesNotContain(AdminNotificationTypes.ApplicationCreated, whTypes);
    }

    [Fact]
    public async Task Dashboard_and_help_show_only_role_modules()
    {
        var hr = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var hrDashboard = await hr.GetStringAsync("/admin");
        Assert.Contains("Hồ sơ ứng tuyển mới", hrDashboard);
        Assert.DoesNotContain("Đơn chờ xử lý", hrDashboard);
        Assert.DoesNotContain("Giá trị kho", hrDashboard);
        Assert.DoesNotContain("chart-gmv", hrDashboard);

        var hrHelp = await hr.GetStringAsync("/admin/Help");
        Assert.Contains("id=\"ho-so\"", hrHelp);
        Assert.Contains("id=\"posts\"", hrHelp);
        Assert.DoesNotContain("id=\"don-hang\"", hrHelp);
        Assert.DoesNotContain("id=\"cai-dat\"", hrHelp);

        var sales = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);
        var salesDashboard = await sales.GetStringAsync("/admin");
        Assert.Contains("Đơn chờ xử lý", salesDashboard);
        Assert.DoesNotContain("Giá trị kho", salesDashboard);
        Assert.DoesNotContain("Hồ sơ ứng tuyển mới", salesDashboard);

        var warehouse = await TestUsers.LoggedInClientAsync(_factory, AppRoles.WarehouseManager);
        Assert.Contains("Giá trị kho", await warehouse.GetStringAsync("/admin"));
        var whHelp = await warehouse.GetStringAsync("/admin/Help");
        Assert.Contains("id=\"kho\"", whHelp);
        Assert.Contains("/admin/InventorySettings", whHelp);
        Assert.DoesNotContain("id=\"ho-so\"", whHelp);
    }

    private static async Task<List<string>> ListTypesAsync(HttpClient client)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/admin/Notifications"));
        return doc.RootElement.EnumerateArray().Select(e => e.GetProperty("type").GetString()!).ToList();
    }

    private static async Task<int> UnreadCountAsync(HttpClient client)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/admin/Notifications/UnreadCount"));
        return doc.RootElement.GetProperty("count").GetInt32();
    }
}
