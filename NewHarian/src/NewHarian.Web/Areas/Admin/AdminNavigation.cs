using System.Security.Claims;
using NewHarian.Application.Abstractions;

namespace NewHarian.Web.Areas.Admin;

public sealed record AdminNavItem(string Label, string Controller, string Action, string Permission, IDictionary<string, string>? RouteData = null)
{
    public IDictionary<string, string> RouteValues { get; } = RouteData ?? new Dictionary<string, string>();
}

public sealed record AdminNavSection(string Label, string Icon, IReadOnlyList<AdminNavItem> Children);

public sealed record AdminNavLink(string Icon, AdminNavItem Item);

public sealed record AdminNavMenu(IReadOnlyList<AdminNavSection> Sections, IReadOnlyList<AdminNavLink> Footer);

/// <summary>Admin sidebar. Each item is shown only when the user holds its permission.</summary>
public static class AdminNavigation
{
    private static readonly IReadOnlyList<AdminNavSection> Sections =
    [
        new("Bán hàng", "fa-bag-shopping",
        [
            new("Đơn hàng", "Orders", "Index", Permissions.Orders.View),
            new("Mã giảm giá", "PromoCodes", "Index", Permissions.PromoCodes.Manage),
            new("Đại lý", "Dealers", "Index", Permissions.Dealers.View),
            new("Chiết khấu đại lý", "DealerDiscounts", "Index", Permissions.Dealers.View),
        ]),
        new("Kho", "fa-warehouse",
        [
            new("Tồn kho", "Inventory", "Index", Permissions.Inventory.View),
            new("Cài đặt kho", "InventorySettings", "Index", Permissions.Inventory.ManageSettings),
        ]),
        new("Sản phẩm", "fa-box",
        [
            new("Danh mục", "Categories", "Index", Permissions.Categories.View),
            new("Sản phẩm", "Products", "Index", Permissions.Products.View),
            new("Màu sắc", "Colors", "Index", Permissions.Colors.View),
        ]),
        new("Dịch vụ", "fa-concierge-bell",
        [
            new("Dịch vụ", "ServiceProducts", "Index", Permissions.Services.View),
            new("Đặt lịch", "ServiceBookings", "Index", Permissions.Bookings.View),
        ]),
        new("Hộp thư", "fa-inbox",
        [
            new("Liên hệ", "Inquiries", "Index", Permissions.Inquiries.View),
            new("Hồ sơ ứng tuyển", "Applications", "Index", Permissions.Applications.View),
        ]),
        new("Nội dung", "fa-newspaper",
        [
            new("Tin tức", "Posts", "Index", Permissions.Posts.Manage, new Dictionary<string, string> { ["kind"] = "News" }),
            new("Tin tuyển dụng", "Posts", "Index", Permissions.Posts.Manage, new Dictionary<string, string> { ["kind"] = "Job" }),
            new("Trang nội dung", "Pages", "Index", Permissions.CmsPages.View),
            new("Menu", "Menus", "Index", Permissions.Menus.Manage),
            new("Slide trang chủ", "HomeSlides", "Index", Permissions.HomeSlides.Manage),
            new("Thư viện ảnh", "Media", "Index", Permissions.Media.Manage),
        ]),
        new("Nhân sự", "fa-users",
        [
            new("Nhân viên", "Employees", "Index", Permissions.Employees.View),
            new("Vị trí công việc", "JobPositions", "Index", Permissions.Employees.Manage),
        ]),
        new("Cài đặt", "fa-gear",
        [
            new("Thương hiệu và logo", "Settings", "Brand", Permissions.SiteSettings.Manage),
            new("Email và thông báo", "Settings", "Email", Permissions.SiteSettings.Manage),
            new("Mẫu email", "EmailTemplates", "Index", Permissions.EmailTemplates.Manage),
            new("Phí ship", "Shipping", "Index", Permissions.Shipping.Manage),
            new("Ngân hàng / QR", "Settings", "Index", Permissions.SiteSettings.Manage),
            new("Tài khoản", "Users", "Index", Permissions.Users.Manage),
        ]),
    ];

    private static readonly IReadOnlyList<AdminNavLink> Footer =
    [
        new("fa-chart-line", new("Thống kê truy cập", "Analytics", "Index", Permissions.Analytics.View)),
        new("fa-circle-question", new("Hướng dẫn", "Help", "Index", Permissions.Help.View)),
    ];

    public static AdminNavMenu For(ClaimsPrincipal user)
    {
        var sections = Sections
            .Select(section => section with
            {
                Children = section.Children.Where(item => user.HasPermission(item.Permission)).ToList()
            })
            .Where(section => section.Children.Count > 0)
            .ToList();
        var footer = Footer.Where(link => user.HasPermission(link.Item.Permission)).ToList();
        return new AdminNavMenu(sections, footer);
    }

    /// <summary>True when this menu item is the page being rendered.</summary>
    public static bool IsCurrent(AdminNavItem item, string? controller, string? action, string? kind)
    {
        if (string.IsNullOrEmpty(controller)
            || !string.Equals(item.Controller, controller, StringComparison.OrdinalIgnoreCase))
            return false;

        if (item.RouteValues.TryGetValue("kind", out var itemKind))
        {
            var currentKind = string.IsNullOrWhiteSpace(kind) ? "News" : kind;
            return string.Equals(itemKind, currentKind, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(item.Controller, "Settings", StringComparison.OrdinalIgnoreCase))
            return string.Equals(item.Action, action, StringComparison.OrdinalIgnoreCase);

        return true;
    }
}
