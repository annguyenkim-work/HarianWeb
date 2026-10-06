using NewHarian.Application.Abstractions;

namespace NewHarian.UnitTests.Application.Abstractions;

[Trait("Category", "Unit")]
public class RolePermissionMapTests
{
    [Fact]
    public void SuperAdmin_holds_every_permission()
    {
        Assert.True(RolePermissionMap.PermissionsOf(AppRoles.SuperAdmin).SetEquals(Permissions.All));
    }

    [Fact]
    public void Every_mapped_permission_exists_in_catalog()
    {
        foreach (var role in RolePermissionMap.InternalRoles)
            Assert.All(RolePermissionMap.PermissionsOf(role), p => Assert.Contains(p, Permissions.All));
    }

    [Fact]
    public void Every_internal_role_can_open_dashboard_help_and_notifications()
    {
        foreach (var role in RolePermissionMap.InternalRoles)
        {
            var set = RolePermissionMap.PermissionsOf(role);
            Assert.Contains(Permissions.Dashboard.View, set);
            Assert.Contains(Permissions.Help.View, set);
            Assert.Contains(Permissions.Notifications.View, set);
        }
    }

    [Fact]
    public void Legacy_admin_is_not_an_internal_role()
    {
        Assert.DoesNotContain(AppRoles.LegacyAdmin, RolePermissionMap.InternalRoles);
        Assert.DoesNotContain(AppRoles.LegacyStaff, AppRoles.Assignable);
    }

    [Fact]
    public void Every_assignable_role_is_internal_and_has_a_label()
    {
        foreach (var role in AppRoles.Assignable)
        {
            Assert.Contains(role, RolePermissionMap.InternalRoles);
            Assert.NotEqual(role, AppRoles.Label(role), StringComparer.Ordinal);
        }
    }

    [Theory]
    [InlineData(Permissions.Orders.Cancel, new[] { AppRoles.SuperAdmin, AppRoles.SalesManager, AppRoles.LegacyStaff })]
    [InlineData(Permissions.Orders.Export, new[] { AppRoles.SuperAdmin, AppRoles.SalesManager, AppRoles.LegacyStaff })]
    [InlineData(Permissions.Orders.ConfirmBankTransfer, new[] { AppRoles.SuperAdmin, AppRoles.SalesManager, AppRoles.LegacyStaff })]
    [InlineData(Permissions.Inventory.Adjust, new[] { AppRoles.SuperAdmin, AppRoles.WarehouseManager, AppRoles.LegacyStaff })]
    [InlineData(Permissions.Inventory.ManageSettings, new[] { AppRoles.SuperAdmin, AppRoles.WarehouseManager })]
    [InlineData(Permissions.Dealers.Approve, new[] { AppRoles.SuperAdmin, AppRoles.SalesManager, AppRoles.LegacyStaff })]
    [InlineData(Permissions.Applications.DownloadCv, new[] { AppRoles.SuperAdmin, AppRoles.HrManager, AppRoles.HrStaff, AppRoles.LegacyStaff })]
    [InlineData(Permissions.CmsPages.EditMeta, new[] { AppRoles.SuperAdmin, AppRoles.HrManager })]
    [InlineData(Permissions.Menus.Manage, new[] { AppRoles.SuperAdmin, AppRoles.HrManager })]
    [InlineData(Permissions.Dashboard.ViewRevenue, new[] { AppRoles.SuperAdmin, AppRoles.SalesManager })]
    [InlineData(Permissions.Users.Manage, new[] { AppRoles.SuperAdmin })]
    [InlineData(Permissions.Products.SuggestVariants, new[] { AppRoles.SuperAdmin, AppRoles.SalesManager, AppRoles.SalesStaff, AppRoles.WarehouseManager, AppRoles.WarehouseStaff, AppRoles.LegacyStaff })]
    public void Sensitive_permissions_match_role_matrix(string permission, string[] expectedRoles)
    {
        var actual = RolePermissionMap.InternalRoles
            .Where(r => RolePermissionMap.PermissionsOf(r).Contains(permission))
            .OrderBy(r => r)
            .ToArray();
        Assert.Equal(expectedRoles.OrderBy(r => r).ToArray(), actual);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData(AppRoles.LegacyAdmin)]
    [InlineData("")]
    public void Unknown_role_has_no_permissions(string role)
    {
        Assert.Empty(RolePermissionMap.PermissionsOf(role));
    }

    [Theory]
    [InlineData(true, AppRoles.HrStaff)]
    [InlineData(true, "Customer", AppRoles.WarehouseStaff)]
    [InlineData(false, "Customer")]
    [InlineData(false, AppRoles.LegacyAdmin)]
    [InlineData(false)]
    public void IsInternalRole_requires_at_least_one_mapped_role(bool expected, params string[] roles)
    {
        Assert.Equal(expected, RolePermissionMap.IsInternalRole(roles));
    }

    [Fact]
    public void HasPermission_is_false_for_anonymous_user()
    {
        Assert.False(TestPrincipals.Anonymous().HasPermission(Permissions.Dashboard.View));
    }

    [Fact]
    public void HasPermission_unions_permissions_of_all_roles()
    {
        var user = TestPrincipals.WithRoles(AppRoles.HrStaff, AppRoles.WarehouseStaff);

        Assert.True(user.HasPermission(Permissions.Applications.DownloadCv));
        Assert.True(user.HasPermission(Permissions.Inventory.Receive));
        Assert.False(user.HasPermission(Permissions.Users.Manage));
        Assert.False(user.HasPermission(Permissions.Inventory.Adjust));
    }

    [Fact]
    public void HasPermission_ignores_roles_outside_the_map()
    {
        var user = TestPrincipals.WithRoles(AppRoles.LegacyAdmin, "Customer");

        Assert.False(user.HasPermission(Permissions.Dashboard.View));
    }
}
