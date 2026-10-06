using System.Reflection;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;

namespace NewHarian.UnitTests.Application.Admin;

[Trait("Category", "Unit")]
public class AdminNotificationTypesTests
{
    private static List<string> DeclaredTypes()
        => typeof(AdminNotificationTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    [Fact]
    public void Every_notification_type_has_a_known_permission()
    {
        var types = DeclaredTypes();

        Assert.NotEmpty(types);
        foreach (var type in types)
        {
            Assert.True(AdminNotificationTypes.RequiredPermission.TryGetValue(type, out var permission), $"{type} not mapped");
            Assert.Contains(permission, Permissions.All);
        }
    }

    [Fact]
    public void Map_has_no_keys_without_a_declared_constant()
    {
        Assert.True(AdminNotificationTypes.RequiredPermission.Keys.ToHashSet().SetEquals(DeclaredTypes()));
    }

    [Theory]
    [InlineData(AppRoles.HrStaff, new[] { AdminNotificationTypes.ApplicationCreated })]
    [InlineData(AppRoles.HrManager, new[] { AdminNotificationTypes.ApplicationCreated })]
    [InlineData(AppRoles.WarehouseStaff, new[] { AdminNotificationTypes.OrderCreated, AdminNotificationTypes.OrderCancelledByGuest })]
    [InlineData(AppRoles.WarehouseManager, new[] { AdminNotificationTypes.OrderCreated, AdminNotificationTypes.OrderCancelledByGuest })]
    [InlineData(AppRoles.SalesStaff, new[]
    {
        AdminNotificationTypes.OrderCreated, AdminNotificationTypes.OrderCancelledByGuest,
        AdminNotificationTypes.ServiceBookingCreated, AdminNotificationTypes.InquiryCreated, AdminNotificationTypes.DealerCreated
    })]
    public void VisibleTo_returns_types_allowed_for_role(string role, string[] expected)
    {
        var visible = AdminNotificationTypes.VisibleTo(TestPrincipals.WithRoles(role));

        Assert.Equal(expected.OrderBy(x => x), visible.OrderBy(x => x));
    }

    [Theory]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.LegacyStaff)]
    public void VisibleTo_returns_every_type_for_full_access_roles(string role)
    {
        var visible = AdminNotificationTypes.VisibleTo(TestPrincipals.WithRoles(role));

        Assert.Equal(DeclaredTypes().OrderBy(x => x), visible.OrderBy(x => x));
    }

    [Fact]
    public void VisibleTo_is_empty_for_anonymous_or_non_internal_users()
    {
        Assert.Empty(AdminNotificationTypes.VisibleTo(TestPrincipals.Anonymous()));
        Assert.Empty(AdminNotificationTypes.VisibleTo(TestPrincipals.WithRoles("Customer")));
    }
}
