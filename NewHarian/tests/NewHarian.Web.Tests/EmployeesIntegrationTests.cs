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
        Assert.Contains($"data-employee-row=\"{staffEmail}\"", list);
        Assert.Contains("Chưa bổ sung hồ sơ", list);

        var form = WebUtility.HtmlDecode(await client.GetStringAsync($"/admin/Employees/Edit/{await UserIdAsync(staffEmail)}"));
        Assert.Contains("Sửa hồ sơ nhân viên", form);
        Assert.Contains("type=\"submit\"", form);
        Assert.DoesNotContain("data-section=\"personal\" disabled", form);
        Assert.DoesNotContain("data-section=\"hr\" disabled", form);
        Assert.Contains("HrNotes", form);
        Assert.Matches("<input[^>]*value=\"Nhân viên Kho\"[^>]*readonly[^>]*data-employee-role", form);
        Assert.DoesNotContain("name=\"Department\"", form);
        Assert.DoesNotContain("name=\"Position\"", form);
    }

    [Fact]
    public async Task Hr_manager_save_returns_success_message_and_assigns_code()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var userId = await UserIdAsync(await TestUsers.EnsureUserAsync(_factory, AppRoles.SalesStaff));
        var token = AdminLoginHelper.ExtractRequestVerificationToken(await client.GetStringAsync($"/admin/Employees/Edit/{userId}"))!;

        var response = await client.PostAsync("/admin/Employees/Save", Form(token, userId, new()
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
        var stored = (await scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>().GetAsync(userId))!;
        Assert.Matches("^NV\\d{4}$", stored.EmployeeCode);
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

        var response = await client.PostAsync("/admin/Employees/Save", Form(token, userId, new()
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
            Content = Form(token, userId, new() { ["FullName"] = "X" })
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
        var token = AdminLoginHelper.ExtractRequestVerificationToken(page)!;

        var response = await client.PostAsync("/admin/MyProfile", Form(token, "someone-else", new()
        {
            ["FullName"] = "Lê Văn Kho",
            ["Phone"] = "0912345678",
            ["DateOfBirth"] = "1998-04-05",
            ["HireDate"] = "2020-01-01",
            ["HrNotes"] = "tự sửa",
            ["Status"] = nameof(EmployeeStatus.Resigned),
        }));

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("admin-toast--success", html);
        Assert.Contains("Đã lưu hồ sơ của bạn.", html);

        using var scope = _factory.Services.CreateScope();
        var stored = (await scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>().GetAsync(userId))!;
        Assert.Equal("Lê Văn Kho", stored.FullName);
        Assert.Equal("0912345678", stored.Phone);
        Assert.Equal(new DateOnly(1998, 4, 5), stored.DateOfBirth);
        Assert.Null(stored.HireDate);
        Assert.Null(stored.HrNotes);
        Assert.Equal(EmployeeStatus.Active, stored.Status);
    }

    [Fact]
    public async Task My_profile_invalid_save_shows_error_toast_and_keeps_input()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SalesStaff);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(await client.GetStringAsync("/admin/MyProfile"))!;

        var response = await client.PostAsync("/admin/MyProfile", Form(token, "", new()
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

    private static FormUrlEncodedContent Form(string token, string userId, Dictionary<string, string> fields)
    {
        fields["UserId"] = userId;
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
