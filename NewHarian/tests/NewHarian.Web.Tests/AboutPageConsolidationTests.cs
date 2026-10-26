using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Cms;
using NewHarian.Infrastructure.Persistence;
using NewHarian.Web.Areas.Admin;

namespace NewHarian.Web.Tests;

/// <summary>Single /about page: legacy URLs redirect; CMS list has no company / concept / quality slugs.</summary>
public class AboutPageConsolidationTests : IClassFixture<NewHarianWebApplicationFactory>, IAsyncLifetime
{
    private readonly NewHarianWebApplicationFactory _factory;

    public AboutPageConsolidationTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbSeeder.SeedCmsOnlyAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task About_page_returns_merged_sections()
    {
        var client = _factory.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/about"));

        // Titles depend on UI culture (Testing often en); accept VI or EN seed copy.
        Assert.True(
            html.Contains("Hồ sơ công ty", StringComparison.Ordinal) || html.Contains("Company profile", StringComparison.Ordinal),
            "Missing company profile section");
        Assert.True(
            html.Contains("Triết lý", StringComparison.Ordinal) || html.Contains("Philosophy", StringComparison.Ordinal),
            "Missing philosophy section");
        Assert.True(
            html.Contains("Cam kết chất lượng", StringComparison.Ordinal) || html.Contains("Quality commitment", StringComparison.Ordinal),
            "Missing quality section");
        Assert.True(
            html.Contains("Hồ sơ năng lực", StringComparison.Ordinal) || html.Contains("Capabilities", StringComparison.Ordinal),
            "Missing capabilities section");
    }

    [Theory]
    [InlineData("/about/concept")]
    [InlineData("/about/quality")]
    [InlineData("/company")]
    public async Task Legacy_about_urls_redirect_permanently_to_about(string path)
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.True(location is "/about" || location.EndsWith("/about", StringComparison.OrdinalIgnoreCase),
            $"Expected Location ending with /about, got '{location}'");
    }

    [Fact]
    public async Task Sitemap_lists_about_without_legacy_slugs()
    {
        var client = _factory.CreateClient();
        var xml = await client.GetStringAsync("/sitemap.xml");

        Assert.Contains("/about", xml);
        Assert.DoesNotContain("/about/concept", xml);
        Assert.DoesNotContain("/about/quality", xml);
        Assert.DoesNotContain("/company</loc>", xml);
    }

    [Fact]
    public async Task Admin_cms_pages_list_has_single_about_no_company_module()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var cms = scope.ServiceProvider.GetRequiredService<IAdminCmsService>();
            var pages = await cms.ListPagesAsync(null);
            Assert.Contains(pages, p => p.Slug == "about" && p.ModuleCode == "about");
            Assert.DoesNotContain(pages, p => p.Slug is "about/concept" or "about/quality" or "company");
            Assert.DoesNotContain(pages, p => p.ModuleCode == "company");
        }

        var client = await TestUsers.LoggedInClientAsync(_factory, AppRoles.HrManager);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/Pages"));
        Assert.Contains("value=\"about\"", html);
        Assert.Contains("Giới thiệu", html);
        Assert.DoesNotContain("value=\"company\"", html);
        Assert.DoesNotContain("about/concept", html);
        Assert.Contains("<code>about</code>", html);
    }

    [Fact]
    public async Task Header_menu_about_is_flat_link()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var menu = db.Menus.First(m => m.Code == "header-main");
        var items = db.MenuItems.Where(i => i.MenuId == menu.Id).ToList();
        var about = items.Single(i => i.ItemKey == "about");
        Assert.Equal("/about", about.Url);
        Assert.Null(about.ParentId);
        Assert.DoesNotContain(items, i => i.ParentId == about.Id);
        Assert.DoesNotContain(items, i => i.ItemKey is "concept" or "quality" or "company" or "about-page");
    }

    [Fact]
    public async Task Public_header_nav_includes_flat_about()
    {
        using var scope = _factory.Services.CreateScope();
        var cms = scope.ServiceProvider.GetRequiredService<ICmsPageService>();
        var nav = await cms.GetHeaderNavAsync("vi");

        var about = Assert.Single(nav, i => i.ItemKey == "about");
        Assert.Equal("/about", about.Url);
        Assert.True(about.Children is null || about.Children.Count == 0);

        var html = WebUtility.HtmlDecode(await _factory.CreateClient().GetStringAsync("/"));
        Assert.Contains("href=\"/about\"", html);
        Assert.DoesNotContain("href=\"/about/concept\"", html);
        Assert.DoesNotContain("href=\"/about/quality\"", html);
    }

    [Theory]
    [InlineData("about", "Giới thiệu")]
    [InlineData("company", "Giới thiệu")]
    public void Cms_module_label_maps_company_to_gioi_thieu(string code, string expected)
        => Assert.Equal(expected, CmsUiLabels.Module(code));
}
