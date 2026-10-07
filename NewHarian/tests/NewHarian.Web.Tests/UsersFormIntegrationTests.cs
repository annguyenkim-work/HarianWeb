using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Infrastructure.Identity;

namespace NewHarian.Web.Tests;

/// <summary>Users modal: single role dropdown + "cần chọn lại role" for multi-role accounts. Own fixture.</summary>
public class UsersFormIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public UsersFormIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task New_user_form_has_role_dropdown_not_checkboxes()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);

        var html = await client.GetStringAsync("/admin/Users/Edit");

        Assert.Contains("<select", html);
        Assert.Contains("name=\"Role\"", html);
        Assert.DoesNotContain("type=\"checkbox\" name=\"Roles\"", html);
        Assert.DoesNotContain($"value=\"{AppRoles.LegacyStaff}\"", html);
    }

    [Fact]
    public async Task Multi_role_user_is_flagged_in_list_and_form_without_preselected_role()
    {
        var id = await CreateMultiRoleUserAsync();
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);

        var list = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/Users"));
        Assert.Contains("Cần chọn lại role", list);

        var form = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Users/Edit/{id}"));
        Assert.Contains("data-role-review", form);
        Assert.DoesNotMatch($"<option[^>]*value=\"{AppRoles.SalesStaff}\"[^>]*selected", form);
    }

    [Fact]
    public async Task Save_without_role_rerenders_form_with_error()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(await client.GetStringAsync("/admin/Users/Edit"))!;

        var response = await client.PostAsync("/admin/Users/Save", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = $"norole-{Guid.NewGuid():N}@test.local",
            ["Password"] = TestUsers.Password,
            ["IsActive"] = "true",
            ["Role"] = "",
            ["__RequestVerificationToken"] = token
        }));

        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("validation-summary-errors", html);
        Assert.Contains(UserRolePolicy.RoleRequired, html);
    }

    private async Task<string> CreateMultiRoleUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"multi-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = email, Email = email, IsActive = true, FullName = "Multi" };
        await users.CreateAsync(user, TestUsers.Password);
        await users.AddToRolesAsync(user, [AppRoles.SalesStaff, AppRoles.WarehouseStaff]);
        return user.Id;
    }
}
