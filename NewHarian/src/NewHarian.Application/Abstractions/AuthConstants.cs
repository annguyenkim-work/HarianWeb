namespace NewHarian.Application.Abstractions;

/// <summary>Internal (Admin area) roles. Codes are stored in AspNetRoles; labels are VI display names.</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string SalesManager = "SalesManager";
    public const string SalesStaff = "SalesStaff";
    public const string WarehouseManager = "WarehouseManager";
    public const string WarehouseStaff = "WarehouseStaff";
    public const string HrManager = "HrManager";
    public const string HrStaff = "HrStaff";

    /// <summary>Pre-RBAC role. Users are moved to <see cref="SuperAdmin"/> by DbSeeder and the role is deleted.</summary>
    public const string LegacyAdmin = "Admin";

    /// <summary>Pre-RBAC role. Keeps its old permissions until Super Admin reassigns each user; cannot be newly assigned.</summary>
    public const string LegacyStaff = "Staff";

    /// <summary>Roles Super Admin can assign on the Users page, in display order.</summary>
    public static IReadOnlyList<string> Assignable { get; } =
    [
        SuperAdmin, SalesManager, SalesStaff, WarehouseManager, WarehouseStaff, HrManager, HrStaff
    ];

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SuperAdmin] = "Super Admin",
        [SalesManager] = "Trưởng phòng Kinh doanh",
        [SalesStaff] = "Nhân viên Bán hàng & CSKH",
        [WarehouseManager] = "Trưởng kho",
        [WarehouseStaff] = "Nhân viên Kho",
        [HrManager] = "Trưởng phòng NS & TT",
        [HrStaff] = "Nhân viên NS & TT",
        [LegacyStaff] = "Staff (cũ)",
    };

    public static string Label(string role) => Labels.TryGetValue(role, out var label) ? label : role;
}

/// <summary>One authorization policy per permission, registered at startup from <see cref="Permissions.All"/>.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "Permission:";

    public static string For(string permission) => Prefix + permission;
}
