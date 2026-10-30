using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Hr;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Tests;

/// <summary>Job titles are an HR catalog. Login roles and account grants stay separate.</summary>
public class JobPositionsIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public JobPositionsIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Hr_manager_adds_a_position_and_assigns_it_without_creating_a_user()
    {
        var hr = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(await hr.GetStringAsync("/admin/JobPositions"))!;
        var name = "Kinh doanh " + Guid.NewGuid().ToString("N")[..6];

        var save = await hr.PostAsync("/admin/JobPositions/Save", Form(token, new()
        {
            ["Name"] = name,
            ["IsActive"] = "true"
        }));
        var created = await save.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(created.GetProperty("ok").GetBoolean());

        var list = WebUtility.HtmlDecode(await hr.GetStringAsync("/admin/JobPositions"));
        Assert.Contains(name, list);
        Assert.Contains("href=\"/admin/JobPositions\"", list, StringComparison.OrdinalIgnoreCase);

        var positionId = await PositionIdAsync(name);
        var employeeToken = AdminLoginHelper.ExtractRequestVerificationToken(await hr.GetStringAsync("/admin/Employees/Create"))!;
        var employee = await hr.PostAsync("/admin/Employees/Save", Form(employeeToken, new()
        {
            ["FullName"] = "Nhân viên chưa có user",
            ["JobPositionId"] = positionId.ToString(),
            ["Status"] = nameof(EmployeeStatus.Active)
        }));
        Assert.True((await employee.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());

        using var scope = _factory.Services.CreateScope();
        var employees = scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>();
        var row = (await employees.ListAsync(name, null)).Single(x => x.FullName == "Nhân viên chưa có user");
        Assert.False(row.HasAccount);
        Assert.Empty(row.Roles);
        Assert.Equal(name, row.JobPositionName);

        var form = WebUtility.HtmlDecode(await hr.GetStringAsync($"/admin/Employees/Edit/{row.ProfileId}"));
        Assert.Contains($"value=\"{positionId}\" selected", form);
        Assert.Contains("Không phải quyền đăng nhập.", form);
    }

    [Fact]
    public async Task Changing_position_does_not_change_role_and_granting_an_account_keeps_the_position()
    {
        var hr = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var first = await AddPositionAsync(hr, "Vị trí A");
        var second = await AddPositionAsync(hr, "Vị trí B");
        var employeeToken = AdminLoginHelper.ExtractRequestVerificationToken(await hr.GetStringAsync("/admin/Employees/Create"))!;
        var created = await hr.PostAsync("/admin/Employees/Save", Form(employeeToken, new()
        {
            ["FullName"] = "Đổi vị trí",
            ["PersonalEmail"] = $"pos-{Guid.NewGuid():N}@test.local",
            ["JobPositionId"] = first.ToString(),
            ["Status"] = nameof(EmployeeStatus.Active)
        }));
        Assert.True((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());

        int profileId;
        string email;
        using (var scope = _factory.Services.CreateScope())
        {
            var row = (await scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>().ListAsync("Đổi vị trí", null))
                .Single(x => x.FullName == "Đổi vị trí");
            profileId = row.ProfileId!.Value;
            email = row.Email;
        }

        var editToken = AdminLoginHelper.ExtractRequestVerificationToken(
            await hr.GetStringAsync($"/admin/Employees/Edit/{profileId}"))!;
        var updated = await hr.PostAsync("/admin/Employees/Save", Form(editToken, new()
        {
            ["Id"] = profileId.ToString(),
            ["FullName"] = "Đổi vị trí",
            ["PersonalEmail"] = email,
            ["JobPositionId"] = second.ToString(),
            ["Status"] = nameof(EmployeeStatus.Active)
        }));
        Assert.True((await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());

        var admin = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var grantToken = AdminLoginHelper.ExtractRequestVerificationToken(
            await admin.GetStringAsync($"/admin/Employees/GrantAccount/{profileId}"))!;
        var grant = await admin.PostAsync("/admin/Employees/GrantAccount", Form(grantToken, new()
        {
            ["ProfileId"] = profileId.ToString(),
            ["Email"] = email,
            ["Password"] = "Staff@123",
            ["Role"] = AppRoles.SalesStaff,
            ["IsActive"] = "true"
        }));
        Assert.True((await grant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());

        using var check = _factory.Services.CreateScope();
        var saved = (await check.ServiceProvider.GetRequiredService<IAdminEmployeeService>().GetByIdAsync(profileId))!;
        Assert.Equal(second, saved.JobPositionId);
        Assert.True(saved.HasAccount);
        Assert.Equal([AppRoles.SalesStaff], saved.Roles);
    }

    [Fact]
    public async Task Inactive_position_stays_on_the_profile_and_drops_out_of_a_new_form()
    {
        var hr = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var activeName = "Đang dùng " + Guid.NewGuid().ToString("N")[..6];
        var retiredName = "Đã tắt " + Guid.NewGuid().ToString("N")[..6];
        var activeId = await AddPositionAsync(hr, activeName);
        var retiredId = await AddPositionAsync(hr, retiredName);

        var createToken = AdminLoginHelper.ExtractRequestVerificationToken(await hr.GetStringAsync("/admin/Employees/Create"))!;
        var created = await hr.PostAsync("/admin/Employees/Save", Form(createToken, new()
        {
            ["FullName"] = "Giữ vị trí tắt",
            ["JobPositionId"] = retiredId.ToString(),
            ["Status"] = nameof(EmployeeStatus.Active)
        }));
        Assert.True((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());

        var editPositionToken = AdminLoginHelper.ExtractRequestVerificationToken(
            await hr.GetStringAsync($"/admin/JobPositions/Edit?id={retiredId}"))!;
        var turnedOff = await hr.PostAsync("/admin/JobPositions/Save", Form(editPositionToken, new()
        {
            ["Id"] = retiredId.ToString(),
            ["Name"] = retiredName,
            ["IsActive"] = "false"
        }));
        Assert.True((await turnedOff.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());

        int profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            profileId = (await scope.ServiceProvider.GetRequiredService<IAdminEmployeeService>().ListAsync(retiredName, null))
                .Single().ProfileId!.Value;
        }

        var edit = WebUtility.HtmlDecode(await hr.GetStringAsync($"/admin/Employees/Edit/{profileId}"));
        Assert.Contains(retiredName, edit);
        Assert.Contains($"value=\"{retiredId}\" selected", edit);

        var fresh = WebUtility.HtmlDecode(await hr.GetStringAsync("/admin/Employees/Create"));
        Assert.Contains(activeName, fresh);
        Assert.DoesNotContain(retiredName, fresh);
        Assert.Contains($"value=\"{activeId}\"", fresh);
    }

    [Fact]
    public async Task Hr_staff_cannot_open_the_position_screen()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrStaff, allowRedirect: false);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/JobPositions");
        request.Headers.Add("Sec-Fetch-Mode", "cors");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);

        var employees = await client.GetStringAsync("/admin/Employees");
        Assert.DoesNotContain("href=\"/admin/JobPositions\"", employees, StringComparison.OrdinalIgnoreCase);

        var help = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/help"));
        Assert.Contains("Vị trí là thông tin nhân sự.", help);
        Assert.Contains("Role", help);
    }

    [Fact]
    public async Task Duplicate_position_name_is_rejected()
    {
        var hr = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var name = "Trùng " + Guid.NewGuid().ToString("N")[..6];
        await AddPositionAsync(hr, name);

        var token = AdminLoginHelper.ExtractRequestVerificationToken(await hr.GetStringAsync("/admin/JobPositions/Edit"))!;
        var response = await hr.PostAsync("/admin/JobPositions/Save", Form(token, new()
        {
            ["Name"] = name.ToUpperInvariant(),
            ["IsActive"] = "true"
        }));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(JobPositionPolicy.NameDuplicate, html);
    }

    private async Task<int> AddPositionAsync(HttpClient hr, string name)
    {
        var token = AdminLoginHelper.ExtractRequestVerificationToken(await hr.GetStringAsync("/admin/JobPositions/Edit"))!;
        var response = await hr.PostAsync("/admin/JobPositions/Save", Form(token, new()
        {
            ["Name"] = name,
            ["IsActive"] = "true"
        }));
        response.EnsureSuccessStatusCode();
        return await PositionIdAsync(name);
    }

    private async Task<int> PositionIdAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IAdminJobPositionService>().ListAsync();
        return rows.Single(p => p.Name == name).Id;
    }

    private static FormUrlEncodedContent Form(string token, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(fields);
    }
}
