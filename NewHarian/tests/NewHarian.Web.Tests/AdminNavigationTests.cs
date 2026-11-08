using System.Security.Claims;
using NewHarian.Application.Abstractions;
using NewHarian.Web.Areas.Admin;

namespace NewHarian.Web.Tests;

/// <summary>Which sidebar item is the page on screen. Lives here because the helper is in the Web project.</summary>
public class AdminNavigationTests
{
    [Theory]
    [InlineData("Categories", "Index", "", true)]
    [InlineData("Products", "Index", "", false)]
    [InlineData("categories", "Edit", "", true)]
    public void Catalog_item_matches_its_controller(string controller, string action, string kind, bool expected)
    {
        var item = new AdminNavItem("Danh mục", "Categories", "Index", "categories.view");

        Assert.Equal(expected, AdminNavigation.IsCurrent(item, controller, action, kind));
    }

    [Theory]
    [InlineData("News", "", true)]
    [InlineData("News", "News", true)]
    [InlineData("News", "Job", false)]
    [InlineData("Job", "", false)]
    [InlineData("Job", "Job", true)]
    public void Post_kind_uses_News_when_the_query_is_empty(string itemKind, string queryKind, bool expected)
    {
        var item = new AdminNavItem("Tin", "Posts", "Index", "posts.manage", new Dictionary<string, string> { ["kind"] = itemKind });

        Assert.Equal(expected, AdminNavigation.IsCurrent(item, "Posts", "Index", queryKind));
    }

    [Theory]
    [InlineData("Brand", "Brand", true)]
    [InlineData("Brand", "Index", false)]
    [InlineData("Email", "Email", true)]
    public void Settings_items_match_the_action(string itemAction, string currentAction, bool expected)
    {
        var item = new AdminNavItem("Thương hiệu", "Settings", itemAction, "settings.manage");

        Assert.Equal(expected, AdminNavigation.IsCurrent(item, "Settings", currentAction, ""));
    }

    [Fact]
    public void SuperAdmin_menu_groups_catalog_away_from_sales_and_keeps_the_footer()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, AppRoles.SuperAdmin)], "test");
        var menu = AdminNavigation.For(new ClaimsPrincipal(identity));

        var sales = Assert.Single(menu.Sections, section => section.Label == "Bán hàng");
        Assert.Contains(sales.Children, item => item.Label == "Đơn hàng");
        Assert.DoesNotContain(sales.Children, item => item.Label == "Danh mục");

        var catalog = Assert.Single(menu.Sections, section => section.Label == "Sản phẩm");
        Assert.Contains(catalog.Children, item => item.Label == "Danh mục");
        Assert.Equal(["Thống kê truy cập", "Hướng dẫn"], menu.Footer.Select(link => link.Item.Label).ToArray());
    }
}
