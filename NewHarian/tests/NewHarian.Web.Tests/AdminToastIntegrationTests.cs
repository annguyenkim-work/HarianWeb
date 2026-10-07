using System.Net;
using System.Text.Json;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;

namespace NewHarian.Web.Tests;

/// <summary>Success / failure toasts after Admin actions. Own fixture (login rate limit).</summary>
public class AdminToastIntegrationTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public AdminToastIntegrationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_layout_loads_toast_script_and_host()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);

        var html = await client.GetStringAsync("/admin");

        Assert.Contains("id=\"admin-toasts\"", html);
        Assert.Contains("/js/admin-toast.js", html);
    }

    [Fact]
    public async Task Redirect_action_success_shows_success_toast_on_next_page()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var token = await TokenAsync(client, "/admin/HomeSlides");

        var response = await client.PostAsync("/admin/HomeSlides/Create", Form(token,
            ("captionVi", "Slide toast test"), ("linkUrl", ""), ("isActive", "true")));

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("admin-toast--success", html);
        Assert.Contains("Đã thêm slide.", html);
    }

    [Fact]
    public async Task Redirect_action_failure_shows_error_toast_with_message()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var token = await TokenAsync(client, "/admin/Menus");

        var response = await client.PostAsync("/admin/Menus/SetActive", Form(token,
            ("itemId", "987654"), ("isActive", "true")));

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("admin-toast--error", html);
        Assert.Contains("Không tìm thấy mục menu.", html);
    }

    [Fact]
    public async Task Json_save_returns_success_message_for_the_toast()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var token = await TokenAsync(client, "/admin/Users");

        var response = await client.PostAsync("/admin/Users/Save", Form(token,
            ("Email", $"toast-{Guid.NewGuid():N}@test.local"),
            ("FullName", "Toast"),
            ("Password", TestUsers.Password),
            ("IsActive", "true"),
            ("Role", AppRoles.SalesStaff)));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("Đã thêm user.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Json_failure_returns_error_message_for_the_toast()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);
        var token = await TokenAsync(client, "/admin/Products");

        var response = await client.PostAsync("/admin/Products/Delete", Form(token, ("id", "987654")));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("Không tìm thấy sản phẩm.", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Invalid_post_rerender_shows_error_toast_with_form_error()
    {
        var client = _factory.CreateClient();
        var token = await TokenAsync(client, "/admin/login");

        var response = await client.PostAsync("/admin/login", Form(token,
            ("Email", "nobody@test.local"), ("Password", "wrong-password"), ("RememberMe", "false")));

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("admin-toast--error", html);
        Assert.Contains("Email hoặc mật khẩu không đúng.", html);
    }

    [Fact]
    public async Task Get_page_without_queued_message_has_no_toast()
    {
        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.SuperAdmin);

        var html = await client.GetStringAsync("/admin/Colors");

        Assert.DoesNotContain("data-server-toast", html);
        Assert.DoesNotContain(AdminToast.InvalidForm, WebUtility.HtmlDecode(html));
    }

    private static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var token = AdminLoginHelper.ExtractRequestVerificationToken(html);
        Assert.False(string.IsNullOrEmpty(token), $"No antiforgery token on {path}");
        return token!;
    }

    private static FormUrlEncodedContent Form(string token, params (string Key, string Value)[] fields)
    {
        var values = fields.ToDictionary(f => f.Key, f => f.Value);
        values["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(values);
    }
}
