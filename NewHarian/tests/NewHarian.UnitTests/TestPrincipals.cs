using System.Security.Claims;

namespace NewHarian.UnitTests;

internal static class TestPrincipals
{
    public static ClaimsPrincipal WithRoles(params string[] roles)
        => new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), authenticationType: "Test"));

    public static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());
}
