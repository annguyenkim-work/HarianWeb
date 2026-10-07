using System.Reflection;

namespace NewHarian.Application.Abstractions;

/// <summary>
/// Admin permission catalog. Which role holds which permission lives only in <see cref="RolePermissionMap"/>.
/// </summary>
public static class Permissions
{
    public static class Dashboard
    {
        public const string View = "Dashboard.View";
        public const string ViewRevenue = "Dashboard.ViewRevenue";
    }

    public static class Help
    {
        public const string View = "Help.View";
    }

    public static class Notifications
    {
        public const string View = "Notifications.View";
    }

    public static class Orders
    {
        public const string View = "Orders.View";
        public const string Create = "Orders.Create";
        public const string Import = "Orders.Import";
        public const string Export = "Orders.Export";
        public const string Print = "Orders.Print";
        public const string ConfirmCod = "Orders.ConfirmCod";
        public const string ConfirmBankTransfer = "Orders.ConfirmBankTransfer";
        public const string UpdateStatus = "Orders.UpdateStatus";
        public const string Cancel = "Orders.Cancel";
        public const string PreviewStockPick = "Orders.PreviewStockPick";
        public const string ViewAllocations = "Orders.ViewAllocations";
    }

    public static class Inventory
    {
        public const string View = "Inventory.View";
        public const string ViewLots = "Inventory.ViewLots";
        public const string ManageLocations = "Inventory.ManageLocations";
        public const string Receive = "Inventory.Receive";
        public const string Adjust = "Inventory.Adjust";
        public const string ViewHistory = "Inventory.ViewHistory";
        public const string ManageSettings = "Inventory.ManageSettings";
    }

    public static class Products
    {
        public const string View = "Products.View";
        public const string Edit = "Products.Edit";
        public const string Reorder = "Products.Reorder";
        public const string UploadImage = "Products.UploadImage";
        public const string Preview = "Products.Preview";
        public const string SuggestVariants = "Products.SuggestVariants";
    }

    public static class Services
    {
        public const string View = "Services.View";
        public const string Edit = "Services.Edit";
        public const string Reorder = "Services.Reorder";
        public const string UploadImage = "Services.UploadImage";
        public const string Preview = "Services.Preview";
    }

    public static class Categories
    {
        public const string View = "Categories.View";
        public const string Edit = "Categories.Edit";
        public const string Reorder = "Categories.Reorder";
    }

    public static class Colors
    {
        public const string View = "Colors.View";
        public const string Edit = "Colors.Edit";
    }

    public static class Bookings
    {
        public const string View = "Bookings.View";
        public const string Update = "Bookings.Update";
    }

    public static class Inquiries
    {
        public const string View = "Inquiries.View";
        public const string Update = "Inquiries.Update";
    }

    public static class Dealers
    {
        public const string View = "Dealers.View";
        public const string Create = "Dealers.Create";
        public const string Approve = "Dealers.Approve";
        public const string Edit = "Dealers.Edit";
    }

    public static class Applications
    {
        public const string View = "Applications.View";
        public const string DownloadCv = "Applications.DownloadCv";
        public const string Update = "Applications.Update";
    }

    public static class Posts
    {
        public const string Manage = "Posts.Manage";
        public const string UploadImage = "Posts.UploadImage";
    }

    public static class CmsPages
    {
        public const string View = "CmsPages.View";
        public const string EditMeta = "CmsPages.EditMeta";
        public const string ManageBlocks = "CmsPages.ManageBlocks";
    }

    public static class Menus
    {
        public const string Manage = "Menus.Manage";
    }

    public static class HomeSlides
    {
        public const string Manage = "HomeSlides.Manage";
    }

    public static class Media
    {
        public const string Manage = "Media.Manage";
    }

    public static class Shipping
    {
        public const string Manage = "Shipping.Manage";
    }

    public static class Users
    {
        public const string Manage = "Users.Manage";
    }

    public static class Employees
    {
        public const string View = "Employees.View";
        public const string Manage = "Employees.Manage";
    }

    /// <summary>Self-service "Hồ sơ của tôi": personal fields of the signed-in user's own employee profile.</summary>
    public static class MyProfile
    {
        public const string Edit = "MyProfile.Edit";
    }

    /// <summary>Bank / VietQR, brand &amp; logo, notification email.</summary>
    public static class SiteSettings
    {
        public const string Manage = "SiteSettings.Manage";
    }

    public static class EmailTemplates
    {
        public const string Manage = "EmailTemplates.Manage";
    }

    public static class Logs
    {
        public const string View = "Logs.View";
    }

    public static class AuditLogs
    {
        public const string View = "AuditLogs.View";
    }

    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();
}
