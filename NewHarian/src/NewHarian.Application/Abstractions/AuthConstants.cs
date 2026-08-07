namespace NewHarian.Application.Abstractions;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
}

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string AdminOrStaff = "AdminOrStaff";
}
