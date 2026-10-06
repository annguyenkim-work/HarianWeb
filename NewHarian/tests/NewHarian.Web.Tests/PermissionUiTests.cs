using System.Net;
using NewHarian.Application.Abstractions;

namespace NewHarian.Web.Tests;

/// <summary>Buttons hidden per permission + fetch() gets 401/403 instead of a redirect. Own fixture (login rate limit).</summary>
public class PermissionUiTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public PermissionUiTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Fetch_to_forbidden_endpoint_gets_403_not_redirect()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff, allowRedirect: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Categories/Edit");
        request.Headers.Add("Sec-Fetch-Mode", "cors");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_fetch_gets_401_not_redirect()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Orders/Detail?id=1");
        request.Headers.Add("Sec-Fetch-Mode", "cors");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sales_staff_sees_read_only_catalog_and_no_order_export()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);

        var orders = await client.GetStringAsync("/admin/Orders");
        Assert.DoesNotContain("/admin/Orders/Export", orders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"btn-add-order\"", orders);

        var categories = await client.GetStringAsync("/admin/Categories");
        Assert.DoesNotContain("id=\"btn-add-cat\"", categories);

        var products = await client.GetStringAsync("/admin/Products");
        Assert.DoesNotContain("id=\"btn-add-prod\"", products);

        var dealers = await client.GetStringAsync("/admin/Dealers");
        Assert.DoesNotContain("id=\"btn-add-dealer\"", dealers);
    }

    [Fact]
    public async Task Sales_manager_sees_management_buttons()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);

        var orders = await client.GetStringAsync("/admin/Orders");
        Assert.Contains("/admin/Orders/Export", orders, StringComparison.OrdinalIgnoreCase);

        var categories = await client.GetStringAsync("/admin/Categories");
        Assert.Contains("id=\"btn-add-cat\"", categories);

        var dealers = await client.GetStringAsync("/admin/Dealers");
        Assert.Contains("id=\"btn-add-dealer\"", dealers);
    }

    [Fact]
    public async Task Manual_order_form_hides_fefo_preview_without_permission()
    {
        var sales = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);
        Assert.DoesNotContain("btn-preview-stock-pick", await sales.GetStringAsync("/admin/Orders/Create"));

        var manager = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        Assert.Contains("btn-preview-stock-pick", await manager.GetStringAsync("/admin/Orders/Create"));
    }

    [Fact]
    public async Task Inventory_receive_button_follows_permission()
    {
        var warehouse = await TestUsers.LoggedInClientAsync(_factory, AppRoles.WarehouseStaff);
        var whHtml = await warehouse.GetStringAsync("/admin/Inventory");
        Assert.Contains("id=\"btn-receive-lot\"", whHtml);

        var manager = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager);
        var smHtml = await manager.GetStringAsync("/admin/Inventory");
        Assert.DoesNotContain("id=\"btn-receive-lot\"", smHtml);
        Assert.Contains("/admin/Inventory/History", smHtml, StringComparison.OrdinalIgnoreCase);
    }
}
