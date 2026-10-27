using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Hr;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Tests;

/// <summary>Nhân viên module + "Hồ sơ của tôi": RBAC, read-only view, toasts. Own fixture (login rate limit).</summary>
public class EmployeesIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public EmployeesIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Hr_manager_sees_menu_list_and_editable_form()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var staffEmail = await TestUsers.EnsureUserAsync(_factory, AppRoles.WarehouseStaff);

        var list = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/Employees"));
        Assert.Contains("href=\"/admin/Employees\"", list, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"role\"", list);
        Assert.Contains("Tất cả role", list);
        Assert.Contains(AppRoles.Label(AppRoles.WarehouseStaff), list);
        Assert.Contains("Chưa bổ sung hồ sơ", list);
        Assert.Contains("btn-add-employee", list);

        var form = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Employees/Edit/{await UserIdAsync(staffEmail)}"));
        Assert.Contains("Sửa hồ sơ nhân viên", form);
        Assert.Contains("type=\"submit\"", form);
        Assert.DoesNotContain("data-section=\"personal\" disabled", form);
        Assert.DoesNotContain("data-section=\"hr\" disabled", form);
        Assert.Contains("HrNotes", form);
        Assert.Matches("<input[^>]*value=\"Nhân viên Kho\"[^>]*readonly[^>]*data-employee-role", form);
        Assert.Contains("Chỉ Super Admin được đổi email.", form);
        Assert.Matches("name=\"PersonalEmail\"[^>]*readonly|readonly[^>]*name=\"PersonalEmail\"", form);
    }

    [Fact]
    public async Task Hr_manager_cannot_change_personal_email_on_existing_profile()
    {
        int profileId;
        var original = $"hr-lock-{Guid.NewGuid():N}@test.local";
        using (var scope = _factory.Services.CreateScope())
        {
            var employees = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
            var (_, _, id) = await employees.SaveAsync(new EmployeeProfileForm
            {
                FullName = "Email Lock HR",
                PersonalEmail = original,
                Status = EmployeeStatus.Active
            }, EmployeeEditScope.Hr, null, canChangeEmail: true);
            profileId = id!.Value;
        }

        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(
            await client.GetStringAsync($"/admin/Employees/Edit/{profileId}"))!;

        var response = await client.PostAsync("/admin/Employees/Save", Form(token, profileId, null, new()
        {
            ["FullName"] = "Email Lock HR",
            ["PersonalEmail"] = $"hacked-{Guid.NewGuid():N}@test.local",
            ["Status"] = nameof(EmployeeStatus.Active),
        }));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), await response.Content.ReadAsStringAsync());

        using var verify = _factory.Services.CreateScope();
        var stored = (await verify.ServiceProvider.GetRequiredService<IAdminEmployeeService>().GetByIdAsync(profileId))!;
        Assert.Equal(original, stored.PersonalEmail);
    }

    [Fact]
    public async Task Super_admin_can_change_personal_email_on_existing_profile()
    {
        int profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var employees = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
            var (_, _, id) = await employees.SaveAsync(new EmployeeProfileForm
            {
                FullName = "Email Unlock SA",
                PersonalEmail = $"sa-old-{Guid.NewGuid():N}@test.local",
                Status = EmployeeStatus.Active
            }, EmployeeEditScope.Hr, null, canChangeEmail: true);
            profileId = id!.Value;
        }

        var next = $"sa-new-{Guid.NewGuid():N}@test.local";
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var editHtml = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Employees/Edit/{profileId}"));
        Assert.DoesNotContain("Chỉ Super Admin được đổi email.", editHtml);
        Assert.DoesNotMatch("name=\"PersonalEmail\"[^>]*readonly|readonly[^>]*name=\"PersonalEmail\"", editHtml);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(editHtml)!;

        var response = await client.PostAsync("/admin/Employees/Save", Form(token, profileId, null, new()
        {
            ["FullName"] = "Email Unlock SA",
            ["PersonalEmail"] = next,
            ["Status"] = nameof(EmployeeStatus.Active),
        }));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), await response.Content.ReadAsStringAsync());

        using var verify = _factory.Services.CreateScope();
        var stored = (await verify.ServiceProvider.GetRequiredService<IAdminEmployeeService>().GetByIdAsync(profileId))!;
        Assert.Equal(next, stored.PersonalEmail);
    }

    [Fact]
    public async Task Hr_manager_create_employee_without_account_assigns_name_code()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(await client.GetStringAsync("/admin/Employees/Create"))!;

        var response = await client.PostAsync("/admin/Employees/Save", Form(token, null, null, new()
        {
            ["FullName"] = "Nguyễn Kim An",
            ["Status"] = nameof(EmployeeStatus.Active),
        }));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), await response.Content.ReadAsStringAsync());
        Assert.Equal("Đã thêm nhân viên.", json.RootElement.GetProperty("message").GetString());

        using var scope = _factory.Services.CreateScope();
        var list = await scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>().ListAsync("AnNK", null);
        Assert.Contains(list, r => r.EmployeeCode is not null && r.EmployeeCode.StartsWith("AnNK") && !r.HasAccount);
    }

    [Fact]
    public async Task Grant_account_form_prefills_email_from_personal_email()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var employees = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
            var email = $"grant-ui-{Guid.NewGuid():N}@test.local";
            var (_, _, id) = await employees.SaveAsync(new EmployeeProfileForm
            {
                FullName = "Võ Thị Grant",
                PersonalEmail = email,
                Status = EmployeeStatus.Active
            }, EmployeeEditScope.Hr, null);

            var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
            var form = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Employees/GrantAccount/{id}"));
            Assert.Contains("Cấp tài khoản", form);
            Assert.Contains($"value=\"{email}\"", form);
            Assert.Contains("readonly", form);
            Assert.Contains("Email cá nhân", form);
        }
    }

    [Fact]
    public async Task Hr_manager_save_returns_success_message_and_assigns_code()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var userId = await UserIdAsync(await TestUsers.EnsureUserAsync(_factory, AppRoles.SalesStaff));
        var token = AdminLoginHelper.ExtractRequestVerificationToken(await client.GetStringAsync($"/admin/Employees/Edit/{userId}"))!;

        var response = await client.PostAsync("/admin/Employees/Save", Form(token, null, userId, new()
        {
            ["FullName"] = "Trần Thị Bán",
            ["HrNotes"] = "Thử việc 2 tháng",
            ["Status"] = nameof(EmployeeStatus.Probation),
            ["HireDate"] = "2026-09-01",
        }));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("Đã lưu hồ sơ nhân viên.", json.RootElement.GetProperty("message").GetString());

        using var scope = _factory.Services.CreateScope();
        var stored = (await scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>().GetByUserIdAsync(userId))!;
        Assert.Equal("BanTT", stored.EmployeeCode);
        Assert.Equal(new DateOnly(2026, 9, 1), stored.HireDate);
        Assert.Equal("Thử việc 2 tháng", stored.HrNotes);
        Assert.Equal(EmployeeStatus.Probation, stored.Status);
    }

    [Fact]
    public async Task Hr_manager_invalid_save_rerenders_form_with_error()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var userId = await UserIdAsync(await TestUsers.EnsureUserAsync(_factory, AppRoles.WarehouseManager));
        var token = AdminLoginHelper.ExtractRequestVerificationToken(await client.GetStringAsync($"/admin/Employees/Edit/{userId}"))!;

        var response = await client.PostAsync("/admin/Employees/Save", Form(token, null, userId, new()
        {
            ["FullName"] = "Kho",
            ["Status"] = nameof(EmployeeStatus.Resigned),
        }));

        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("validation-summary-errors", html);
        Assert.Contains(EmployeeProfilePolicy.TerminationRequired, html);
    }

    [Fact]
    public async Task Hr_staff_can_view_read_only_but_cannot_save()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrStaff, allowRedirect: false);
        var userId = await UserIdAsync(await TestUsers.EnsureUserAsync(_factory, AppRoles.SalesManager));

        var list = await client.GetStringAsync("/admin/Employees");
        Assert.Contains("btn-open-employee", list);
        Assert.Contains("aria-label=\"Xem\"", list);

        var form = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Employees/Edit/{userId}"));
        Assert.DoesNotContain("type=\"submit\"", form);
        Assert.Contains("data-section=\"personal\" disabled", form);
        Assert.Contains("data-section=\"hr\" disabled", form);

        var token = AdminLoginHelper.ExtractRequestVerificationToken(await client.GetStringAsync("/admin/MyProfile"))!;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/Employees/Save")
        {
            Content = Form(token, null, userId, new() { ["FullName"] = "X" })
        };
        request.Headers.Add("Sec-Fetch-Mode", "cors");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Non_hr_role_cannot_open_employees_and_menu_is_hidden()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesManager, allowRedirect: false);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/Employees");
        request.Headers.Add("Sec-Fetch-Mode", "cors");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);

        var home = await client.GetStringAsync("/admin");
        Assert.DoesNotContain("href=\"/admin/Employees\"", home, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"admin-my-profile\"", home);
    }

    [Fact]
    public async Task My_profile_saves_personal_fields_ignores_hr_fields_and_shows_success_toast()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.WarehouseStaff);
        var email = await TestUsers.EnsureUserAsync(_factory, AppRoles.WarehouseStaff);
        var userId = await UserIdAsync(email);

        var page = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/MyProfile"));
        Assert.Contains("data-section=\"hr\" disabled", page);
        Assert.DoesNotContain("data-section=\"personal\" disabled", page);
        Assert.DoesNotContain("HrNotes", page);
        Assert.Contains("Chỉ Super Admin được đổi email.", page);
        Assert.Matches("name=\"PersonalEmail\"[^>]*readonly|readonly[^>]*name=\"PersonalEmail\"", page);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(page)!;

        var response = await client.PostAsync("/admin/MyProfile", Form(token, null, "someone-else", new()
        {
            ["FullName"] = "Lê Văn Kho",
            ["Phone"] = "0912345678",
            ["PersonalEmail"] = $"self-hack-{Guid.NewGuid():N}@test.local",
            ["DateOfBirth"] = "1998-04-05",
            ["HireDate"] = "2020-01-01",
            ["HrNotes"] = "tự sửa",
            ["Status"] = nameof(EmployeeStatus.Resigned),
        }));

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("admin-toast--success", html);
        Assert.Contains("Đã lưu hồ sơ của bạn.", html);

        using var scope = _factory.Services.CreateScope();
        var stored = (await scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>().GetByUserIdAsync(userId))!;
        Assert.Equal("Lê Văn Kho", stored.FullName);
        Assert.Equal("0912345678", stored.Phone);
        Assert.Equal(new DateOnly(1998, 4, 5), stored.DateOfBirth);
        Assert.Null(stored.HireDate);
        Assert.Null(stored.HrNotes);
        Assert.Equal(EmployeeStatus.Active, stored.Status);
        Assert.Equal("KhoLV", stored.EmployeeCode);
        Assert.Null(stored.PersonalEmail);
    }

    [Fact]
    public async Task My_profile_invalid_save_shows_error_toast_and_keeps_input()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(await client.GetStringAsync("/admin/MyProfile"))!;

        var response = await client.PostAsync("/admin/MyProfile", Form(token, null, "", new()
        {
            ["FullName"] = "Bán Hàng",
            ["Phone"] = "0912345678",
            ["CitizenId"] = "12345",
        }));

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("admin-toast--error", html);
        Assert.Contains(EmployeeProfilePolicy.CitizenIdInvalid, html);
        Assert.Contains("value=\"0912345678\"", html);
    }

    private static FormUrlEncodedContent Form(string token, int? id, string? userId, Dictionary<string, string> fields)
    {
        if (id is not null) fields["Id"] = id.Value.ToString();
        if (userId is not null) fields["UserId"] = userId;
        fields["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(fields);
    }

    private async Task<string> UserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<NewHarian.Infrastructure.Identity.ApplicationUser>>();
        return (await users.FindByEmailAsync(email))!.Id;
    }
}
