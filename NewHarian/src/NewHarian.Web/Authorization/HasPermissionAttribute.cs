using Microsoft.AspNetCore.Authorization;
using NewHarian.Application.Abstractions;

namespace NewHarian.Web.Authorization;

/// <summary>
/// Requires a <see cref="Permissions"/> entry. Class + action attributes are AND-ed, so prefer one level per controller.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicy.For(permission))
{
    public string Permission { get; } = permission;
}
