using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Hr;
using NewHarian.Domain.Enums;
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
    public async Task New_user_form_employee_options_include_personal_email_data()
    {
        var email = $"link-ui-{Guid.NewGuid():N}@test.local";
        using (var scope = _factory.Services.CreateScope())
        {
            var employees = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
            await employees.SaveAsync(new EmployeeProfileForm
            {
                FullName = "Đặng Văn Link",
                PersonalEmail = email
            }, EmployeeEditScope.Hr, null);
        }

        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/Users/Edit"));

        Assert.Contains("data-employee-link", html);
        Assert.Contains($"data-email=\"{email}\"", html);
        Assert.Contains("data-fullname=\"Đặng Văn Link\"", html);
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

    [Fact]
    public async Task Super_admin_can_change_login_email_and_syncs_linked_personal_email()
    {
        var oldEmail = $"user-old-{Guid.NewGuid():N}@test.local";
        var newEmail = $"user-new-{Guid.NewGuid():N}@test.local";
        string userId;
        int profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            await TestUsers.EnsureRolesAsync(scope.ServiceProvider);
            var employees = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
            var (_, _, id) = await employees.SaveAsync(new EmployeeProfileForm
            {
                FullName = "Đổi Email User",
                PersonalEmail = oldEmail,
                Status = EmployeeStatus.Active
            }, EmployeeEditScope.Hr, null, canChangeEmail: true);
            profileId = id!.Value;

            var users = scope.ServiceProvider.GetRequiredService<IAdminUserService>();
            var (ok, error, createdId) = await users.SaveAsync(new AdminUserSaveRequest
            {
                Email = oldEmail,
                Password = TestUsers.Password,
                Role = AppRoles.SalesStaff,
                IsActive = true,
                EmployeeProfileId = profileId
            }, actorUserId: null);
            Assert.True(ok, error);
            userId = createdId!;
        }

        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var form = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Users/Edit/{userId}"));
        Assert.Contains("data-user-email", form);
        Assert.DoesNotContain("Chỉ Super Admin được đổi email.", form);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(form)!;

        var response = await client.PostAsync("/admin/Users/Save", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = userId,
            ["Email"] = newEmail,
            ["Role"] = AppRoles.SalesStaff,
            ["IsActive"] = "true",
            ["EmployeeProfileId"] = profileId.ToString(),
            ["__RequestVerificationToken"] = token
        }));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), await response.Content.ReadAsStringAsync());

        using var verify = _factory.Services.CreateScope();
        var um = verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await um.FindByIdAsync(userId);
        Assert.Equal(newEmail, user!.Email);
        var profile = (await verify.ServiceProvider.GetRequiredService<IAdminEmployeeService>().GetByIdAsync(profileId))!;
        Assert.Equal(newEmail, profile.PersonalEmail);
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
