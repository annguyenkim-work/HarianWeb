using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NewHarian.Application.Abstractions;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Tests;

public class PermissionAuthorizationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public PermissionAuthorizationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void Every_admin_action_requires_a_permission()
    {
        var controllers = typeof(HasPermissionAttribute).Assembly.GetTypes()
            .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<AreaAttribute>()?.RouteValue == "Admin");

        var unguarded = new List<string>();
        foreach (var controller in controllers)
        {
            var classGuarded = controller.GetCustomAttributes<HasPermissionAttribute>().Any();
            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

            foreach (var action in actions)
            {
                if (action.GetCustomAttribute<AllowAnonymousAttribute>() is not null) continue;
                if (classGuarded || action.GetCustomAttributes<HasPermissionAttribute>().Any()) continue;
                if (controller.Name == "AccountController" && action.GetCustomAttribute<AuthorizeAttribute>() is not null) continue;
                unguarded.Add($"{controller.Name}.{action.Name}");
            }
        }

        Assert.True(unguarded.Count == 0, "Missing [HasPermission]: " + string.Join(", ", unguarded));
    }

    [Fact]
    public void Admin_hubs_require_a_permission()
    {
        var hubs = typeof(HasPermissionAttribute).Assembly.GetTypes()
            .Where(t => typeof(Hub).IsAssignableFrom(t) && !t.IsAbstract);
        Assert.All(hubs, h => Assert.NotEmpty(h.GetCustomAttributes<HasPermissionAttribute>()));
    }

    [Fact]
    public async Task Menu_follows_role_permissions()
    {
        var hr = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrStaff);
        var hrHtml = await hr.GetStringAsync("/admin/Help");
        Assert.Contains(Href("/admin/Applications"), hrHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kind=Job", hrHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Href("/admin/Orders"), hrHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Href("/admin/Menus"), hrHtml, StringComparison.OrdinalIgnoreCase);

        var warehouse = await TestUsers.LoggedInClientAsync(_factory, AppRoles.WarehouseManager);
        var whHtml = await warehouse.GetStringAsync("/admin/Help");
        Assert.Contains(Href("/admin/Inventory"), whHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Href("/admin/InventorySettings"), whHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Href("/admin/Settings/Brand"), whHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Href("/admin/Applications"), whHtml, StringComparison.OrdinalIgnoreCase);

        var admin = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var adminHtml = await admin.GetStringAsync("/admin/Help");
        Assert.Contains(Href("/admin/Categories"), adminHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Href("/admin/Users"), adminHtml, StringComparison.OrdinalIgnoreCase);
    }

    private static string Href(string path) => $"href=\"{path}\"";

    [Theory]
    [InlineData(AppRoles.LegacyStaff, "/admin/Categories")]
    [InlineData(AppRoles.LegacyStaff, "/admin/Users")]
    [InlineData(AppRoles.LegacyStaff, "/admin/InventorySettings")]
    [InlineData(AppRoles.WarehouseManager, "/admin/Settings")]
    [InlineData(AppRoles.WarehouseStaff, "/admin/Inventory/Adjust/1")]
    [InlineData(AppRoles.WarehouseStaff, "/admin/Orders/Export")]
    [InlineData(AppRoles.SalesStaff, "/admin/Orders/Export")]
    [InlineData(AppRoles.SalesStaff, "/admin/Applications/Cv/1")]
    [InlineData(AppRoles.HrStaff, "/admin/Orders")]
    [InlineData(AppRoles.SalesManager, "/admin/Users")]
    public async Task Role_is_denied_endpoints_outside_matrix(string role, string url)
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, role, allowRedirect: false);
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/access-denied", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sales_staff_cannot_cancel_order_through_status_update()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);
        var page = await client.GetStringAsync("/admin/Help");
        var token = AdminLoginHelper.ExtractRequestVerificationToken(page);
        Assert.False(string.IsNullOrEmpty(token));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "999999",
            ["status"] = "Cancelled",
            ["__RequestVerificationToken"] = token!
        });
        var response = await client.PostAsync("/admin/Orders/UpdateStatus", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("quyền hủy", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Anonymous_is_sent_to_login()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/admin/Orders");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/login", response.Headers.Location?.OriginalString ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
