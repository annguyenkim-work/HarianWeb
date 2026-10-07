using NewHarian.Application.Abstractions;

namespace NewHarian.Application.Admin;

/// <summary>
/// One internal account = exactly one role. Accounts that still hold several roles (from before this rule) keep
/// their combined permissions until an admin picks one role in the edit form.
/// </summary>
public static class UserRolePolicy
{
    public const string RoleRequired = "Chọn role.";
    public const string RoleInvalid = "Role không hợp lệ.";

    /// <summary>
    /// Validates the role picked in the form. Legacy Staff is accepted only while the user already holds it,
    /// so it can be kept but never newly assigned.
    /// </summary>
    public static (string? Role, string? Error) Validate(string? requested, IReadOnlyCollection<string> currentRoles)
    {
        if (string.IsNullOrWhiteSpace(requested)) return (null, RoleRequired);

        var role = requested.Trim();
        if (AppRoles.Assignable.Contains(role, StringComparer.Ordinal)) return (role, null);
        if (role == AppRoles.LegacyStaff && currentRoles.Contains(AppRoles.LegacyStaff)) return (role, null);
        return (null, RoleInvalid);
    }

    /// <summary>More than one role: shown as "Cần chọn lại role" until an admin saves a single role.</summary>
    public static bool NeedsRoleReview(IReadOnlyCollection<string> roles) => roles.Count > 1;

    /// <summary>Role pre-selected in the edit form; null forces the admin to choose.</summary>
    public static string? PreselectedRole(IReadOnlyCollection<string> roles) => roles.Count == 1 ? roles.First() : null;

    /// <summary>Dropdown options: assignable roles, plus Legacy Staff only for a user who still holds it.</summary>
    public static IReadOnlyList<string> Options(IReadOnlyCollection<string> currentRoles)
        => currentRoles.Contains(AppRoles.LegacyStaff)
            ? AppRoles.Assignable.Append(AppRoles.LegacyStaff).ToList()
            : AppRoles.Assignable;
}
