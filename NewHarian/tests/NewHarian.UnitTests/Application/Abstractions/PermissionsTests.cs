using System.Reflection;
using NewHarian.Application.Abstractions;

namespace NewHarian.UnitTests.Application.Abstractions;

[Trait("Category", "Unit")]
public class PermissionsTests
{
    private static IEnumerable<(Type Group, string Code)> DeclaredCodes()
        => typeof(Permissions)
            .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (t, (string)f.GetRawConstantValue()!)));

    [Fact]
    public void Permission_codes_are_unique()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(Permissions.Orders.Export, Permissions.All);
    }

    [Fact]
    public void All_contains_every_declared_constant()
    {
        var declared = DeclaredCodes().Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        Assert.True(declared.SetEquals(Permissions.All));
    }

    [Fact]
    public void Every_code_is_prefixed_with_its_group_name()
    {
        Assert.All(DeclaredCodes(), x => Assert.StartsWith(x.Group.Name + ".", x.Code, StringComparison.Ordinal));
    }

    [Fact]
    public void Policy_name_is_prefix_plus_permission()
    {
        Assert.Equal("Permission:Orders.View", PermissionPolicy.For(Permissions.Orders.View));
    }
}
