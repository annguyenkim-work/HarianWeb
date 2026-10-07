using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;

namespace NewHarian.UnitTests.Application.Admin;

[Trait("Category", "Unit")]
public class UserRolePolicyTests
{
    public static TheoryData<string> AssignableRoles()
    {
        var data = new TheoryData<string>();
        foreach (var role in AppRoles.Assignable) data.Add(role);
        return data;
    }

    [Theory]
    [MemberData(nameof(AssignableRoles))]
    public void Every_assignable_role_is_accepted(string role)
        => Assert.Equal((role, (string?)null), UserRolePolicy.Validate(role, []));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Missing_role_is_rejected(string? role)
        => Assert.Equal(((string?)null, UserRolePolicy.RoleRequired), UserRolePolicy.Validate(role, []));

    [Theory]
    [InlineData("Admin")]
    [InlineData("superadmin")]
    [InlineData("Guest")]
    public void Unknown_or_wrong_case_role_is_rejected(string role)
        => Assert.Equal(((string?)null, UserRolePolicy.RoleInvalid), UserRolePolicy.Validate(role, []));

    [Fact]
    public void Role_is_trimmed()
        => Assert.Equal(AppRoles.HrStaff, UserRolePolicy.Validate($"  {AppRoles.HrStaff} ", []).Role);

    [Fact]
    public void Legacy_staff_is_kept_only_for_a_user_who_holds_it()
    {
        Assert.Equal(UserRolePolicy.RoleInvalid, UserRolePolicy.Validate(AppRoles.LegacyStaff, [AppRoles.SalesStaff]).Error);
        Assert.Equal(AppRoles.LegacyStaff, UserRolePolicy.Validate(AppRoles.LegacyStaff, [AppRoles.LegacyStaff]).Role);
    }

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { AppRoles.SalesStaff }, false)]
    [InlineData(new[] { AppRoles.LegacyStaff }, false)]
    [InlineData(new[] { AppRoles.WarehouseStaff, AppRoles.LegacyStaff }, true)]
    [InlineData(new[] { AppRoles.SuperAdmin, AppRoles.HrManager }, true)]
    public void Needs_review_only_with_more_than_one_role(string[] roles, bool expected)
        => Assert.Equal(expected, UserRolePolicy.NeedsRoleReview(roles));

    [Theory]
    [InlineData(new string[0], null)]
    [InlineData(new[] { AppRoles.SalesManager }, AppRoles.SalesManager)]
    [InlineData(new[] { AppRoles.SalesManager, AppRoles.SalesStaff }, null)]
    public void Preselects_only_a_single_role(string[] roles, string? expected)
        => Assert.Equal(expected, UserRolePolicy.PreselectedRole(roles));

    [Fact]
    public void Options_offer_legacy_staff_only_to_its_holders()
    {
        Assert.Equal(AppRoles.Assignable, UserRolePolicy.Options([AppRoles.SalesStaff]));
        var legacy = UserRolePolicy.Options([AppRoles.LegacyStaff, AppRoles.WarehouseStaff]);
        Assert.Equal(AppRoles.Assignable.Append(AppRoles.LegacyStaff), legacy);
    }
}
