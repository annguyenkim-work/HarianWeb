using System.Security.Claims;
using NewHarian.Application.Abstractions;

namespace NewHarian.Web.Areas.Admin;

public sealed record AdminNavItem(string Label, string Controller, string Action, string Permission, IDictionary<string, string>? RouteData = null)
{
    public IDictionary<string, string> RouteValues { get; } = RouteData ?? new Dictionary<string, string>();
}

public sealed record AdminNavGroup(string Title, IReadOnlyList<AdminNavItem> Items);

/// <summary>Admin header + sidebar menu. Each item is shown only when the user holds its permission.</summary>
public static class AdminNavigation
{
    private static readonly IReadOnlyList<AdminNavGroup> Groups =
    [
        new("Vận hành",
        [
            new("Danh mục", "Categories", "Index", Permissions.Categories.View),
            new("Sản phẩm", "Products", "Index", Permissions.Products.View),
            new("Dịch vụ", "ServiceProducts", "Index", Permissions.Services.View),
            new("Màu sắc", "Colors", "Index", Permissions.Colors.View),
            new("Đặt lịch", "ServiceBookings", "Index", Permissions.Bookings.View),
            new("Đơn hàng", "Orders", "Index", Permissions.Orders.View),
            new("Kho", "Inventory", "Index", Permissions.Inventory.View),
            new("Liên hệ", "Inquiries", "Index", Permissions.Inquiries.View),
            new("Đại lý", "Dealers", "Index", Permissions.Dealers.View),
            new("Hồ sơ ứng tuyển", "Applications", "Index", Permissions.Applications.View),
            new("Hướng dẫn", "Help", "Index", Permissions.Help.View),
            new("Tin tức", "Posts", "Index", Permissions.Posts.Manage, new Dictionary<string, string> { ["kind"] = "News" }),
            new("Tin tuyển dụng", "Posts", "Index", Permissions.Posts.Manage, new Dictionary<string, string> { ["kind"] = "Job" }),
        ]),
        new("Cài đặt website",
        [
            new("Trang nội dung", "Pages", "Index", Permissions.CmsPages.View),
            new("Menus", "Menus", "Index", Permissions.Menus.Manage),
            new("Slides", "HomeSlides", "Index", Permissions.HomeSlides.Manage),
            new("Media", "Media", "Index", Permissions.Media.Manage),
            new("Phí ship", "Shipping", "Index", Permissions.Shipping.Manage),
            new("Users", "Users", "Index", Permissions.Users.Manage),
            new("Thương hiệu & logo", "Settings", "Brand", Permissions.SiteSettings.Manage),
            new("Email & thông báo", "Settings", "Email", Permissions.SiteSettings.Manage),
            new("Mẫu email", "EmailTemplates", "Index", Permissions.EmailTemplates.Manage),
            new("Kho", "InventorySettings", "Index", Permissions.Inventory.ManageSettings),
            new("Ngân hàng / QR", "Settings", "Index", Permissions.SiteSettings.Manage),
        ]),
    ];

    public static IReadOnlyList<AdminNavGroup> For(ClaimsPrincipal user)
        => Groups
            .Select(g => g with { Items = g.Items.Where(i => user.HasPermission(i.Permission)).ToList() })
            .Where(g => g.Items.Count > 0)
            .ToList();
}
