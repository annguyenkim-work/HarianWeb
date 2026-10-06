using System.Security.Claims;

namespace NewHarian.Application.Abstractions;

/// <summary>
/// Single source of truth for role → permissions, translated from Phan_Quyen_He_Thong_Role_Matrix_v2.xlsx.
/// A user with several roles gets the union. Public (guest) features are not permission-gated.
/// </summary>
public static class RolePermissionMap
{
    private static readonly string[] EveryInternalUser =
    [
        Permissions.Dashboard.View,
        Permissions.Help.View,
        Permissions.Notifications.View,
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Map =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [AppRoles.SuperAdmin] = Permissions.All.ToHashSet(StringComparer.Ordinal),

            [AppRoles.SalesManager] = Set(
                Permissions.Dashboard.ViewRevenue,
                Permissions.Orders.View,
                Permissions.Orders.Create,
                Permissions.Orders.Import,
                Permissions.Orders.Export,
                Permissions.Orders.Print,
                Permissions.Orders.ConfirmCod,
                Permissions.Orders.ConfirmBankTransfer,
                Permissions.Orders.UpdateStatus,
                Permissions.Orders.Cancel,
                Permissions.Orders.PreviewStockPick,
                Permissions.Orders.ViewAllocations,
                Permissions.Inventory.View,
                Permissions.Inventory.ViewHistory,
                Permissions.Products.View,
                Permissions.Products.Edit,
                Permissions.Products.Reorder,
                Permissions.Products.UploadImage,
                Permissions.Products.Preview,
                Permissions.Products.SuggestVariants,
                Permissions.Services.View,
                Permissions.Services.Edit,
                Permissions.Services.Reorder,
                Permissions.Services.UploadImage,
                Permissions.Services.Preview,
                Permissions.Categories.View,
                Permissions.Categories.Edit,
                Permissions.Categories.Reorder,
                Permissions.Colors.View,
                Permissions.Colors.Edit,
                Permissions.Bookings.View,
                Permissions.Bookings.Update,
                Permissions.Inquiries.View,
                Permissions.Inquiries.Update,
                Permissions.Dealers.View,
                Permissions.Dealers.Create,
                Permissions.Dealers.Approve,
                Permissions.Dealers.Edit),

            // Matrix denies SKU suggest to Sales Staff, but the manual order form needs it; FEFO preview stays hidden.
            [AppRoles.SalesStaff] = Set(
                Permissions.Orders.View,
                Permissions.Orders.Create,
                Permissions.Orders.Import,
                Permissions.Orders.Print,
                Permissions.Orders.ConfirmCod,
                Permissions.Orders.UpdateStatus,
                Permissions.Orders.ViewAllocations,
                Permissions.Products.View,
                Permissions.Products.Preview,
                Permissions.Products.SuggestVariants,
                Permissions.Services.View,
                Permissions.Services.Preview,
                Permissions.Categories.View,
                Permissions.Colors.View,
                Permissions.Bookings.View,
                Permissions.Bookings.Update,
                Permissions.Inquiries.View,
                Permissions.Inquiries.Update,
                Permissions.Dealers.View),

            [AppRoles.WarehouseManager] = Set(
                Permissions.Orders.View,
                Permissions.Orders.Print,
                Permissions.Orders.UpdateStatus,
                Permissions.Orders.PreviewStockPick,
                Permissions.Orders.ViewAllocations,
                Permissions.Inventory.View,
                Permissions.Inventory.ViewLots,
                Permissions.Inventory.ManageLocations,
                Permissions.Inventory.Receive,
                Permissions.Inventory.Adjust,
                Permissions.Inventory.ViewHistory,
                Permissions.Inventory.ManageSettings,
                Permissions.Products.View,
                Permissions.Products.SuggestVariants,
                Permissions.Categories.View,
                Permissions.Colors.View),

            [AppRoles.WarehouseStaff] = Set(
                Permissions.Orders.View,
                Permissions.Orders.Print,
                Permissions.Orders.UpdateStatus,
                Permissions.Orders.PreviewStockPick,
                Permissions.Orders.ViewAllocations,
                Permissions.Inventory.View,
                Permissions.Inventory.ViewLots,
                Permissions.Inventory.Receive,
                Permissions.Inventory.ViewHistory,
                Permissions.Products.View,
                Permissions.Products.SuggestVariants,
                Permissions.Colors.View),

            [AppRoles.HrManager] = Set(
                Permissions.Products.View,
                Permissions.Applications.View,
                Permissions.Applications.DownloadCv,
                Permissions.Applications.Update,
                Permissions.Posts.Manage,
                Permissions.Posts.UploadImage,
                Permissions.CmsPages.View,
                Permissions.CmsPages.EditMeta,
                Permissions.CmsPages.ManageBlocks,
                Permissions.Menus.Manage,
                Permissions.HomeSlides.Manage),

            [AppRoles.HrStaff] = Set(
                Permissions.Products.View,
                Permissions.Applications.View,
                Permissions.Applications.DownloadCv,
                Permissions.Applications.Update,
                Permissions.Posts.Manage,
                Permissions.Posts.UploadImage,
                Permissions.CmsPages.View,
                Permissions.CmsPages.ManageBlocks,
                Permissions.HomeSlides.Manage),

            // Pre-RBAC Staff: unchanged access until reassigned.
            [AppRoles.LegacyStaff] = Set(
                Permissions.Orders.View,
                Permissions.Orders.Create,
                Permissions.Orders.Import,
                Permissions.Orders.Export,
                Permissions.Orders.Print,
                Permissions.Orders.ConfirmCod,
                Permissions.Orders.ConfirmBankTransfer,
                Permissions.Orders.UpdateStatus,
                Permissions.Orders.Cancel,
                Permissions.Orders.PreviewStockPick,
                Permissions.Orders.ViewAllocations,
                Permissions.Inventory.View,
                Permissions.Inventory.ViewLots,
                Permissions.Inventory.Receive,
                Permissions.Inventory.Adjust,
                Permissions.Inventory.ViewHistory,
                Permissions.Products.SuggestVariants,
                Permissions.Bookings.View,
                Permissions.Bookings.Update,
                Permissions.Inquiries.View,
                Permissions.Inquiries.Update,
                Permissions.Dealers.View,
                Permissions.Dealers.Create,
                Permissions.Dealers.Approve,
                Permissions.Dealers.Edit,
                Permissions.Applications.View,
                Permissions.Applications.DownloadCv,
                Permissions.Applications.Update),
        };

    /// <summary>Roles allowed to sign in to Admin.</summary>
    public static IReadOnlyCollection<string> InternalRoles { get; } = Map.Keys.ToArray();

    public static IReadOnlySet<string> PermissionsOf(string role)
        => Map.TryGetValue(role, out var set) ? set : new HashSet<string>();

    public static bool IsInternalRole(IEnumerable<string> roles) => roles.Any(Map.ContainsKey);

    public static bool HasPermission(this ClaimsPrincipal user, string permission)
        => user.Identity?.IsAuthenticated == true
           && Map.Any(kv => kv.Value.Contains(permission) && user.IsInRole(kv.Key));

    private static HashSet<string> Set(params string[] permissions)
        => new(EveryInternalUser.Concat(permissions), StringComparer.Ordinal);
}
