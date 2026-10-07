using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;

namespace NewHarian.Infrastructure.Identity;

public sealed class AdminUserService(
    UserManager<ApplicationUser> users,
    IAuditService audit,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    public async Task<IReadOnlyList<AdminUserListItemDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        var rows = new List<AdminUserListItemDto>(list.Count);
        foreach (var u in list)
        {
            var roles = await users.GetRolesAsync(u);
            rows.Add(new AdminUserListItemDto(u.Id, u.Email ?? "", u.FullName, u.IsActive, OrderRoles(roles), u.CreatedAt));
        }
        return rows;
    }

    public async Task<AdminUserSaveRequest?> GetForEditAsync(string id, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return null;
        var roles = OrderRoles(await users.GetRolesAsync(user));
        return new AdminUserSaveRequest
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName,
            IsActive = user.IsActive,
            Role = UserRolePolicy.PreselectedRole(roles),
            CurrentRoles = roles.ToList()
        };
    }

    public async Task<(bool Ok, string? Error, string? Id)> SaveAsync(
        AdminUserSaveRequest request, string? actorUserId, CancellationToken ct = default)
    {
        logger.LogInformation("SaveUser Start Id={Id} Email={Email}", request.Id, request.Email);
        try
        {
            var (ok, error, id) = request.Id is null
                ? await CreateAsync(request)
                : await UpdateAsync(request, actorUserId);

            if (!ok)
            {
                logger.LogWarning("SaveUser Done rejected Id={Id} Email={Email} Error={Error}", request.Id, request.Email, error);
                return (false, error, null);
            }

            logger.LogInformation("SaveUser Done Id={Id} Role={Role} IsActive={IsActive}",
                id, request.Role, request.IsActive);
            return (true, null, id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveUser Error Id={Id} Email={Email}", request.Id, request.Email);
            throw;
        }
    }

    private async Task<(bool Ok, string? Error, string? Id)> CreateAsync(AdminUserSaveRequest request)
    {
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email)) return (false, "Email bắt buộc.", null);
        if (string.IsNullOrWhiteSpace(request.Password)) return (false, "Mật khẩu bắt buộc khi tạo user.", null);
        if (await users.FindByEmailAsync(email) is not null) return (false, "Email đã được dùng cho user khác.", null);

        var (role, roleError) = UserRolePolicy.Validate(request.Role, currentRoles: []);
        if (role is null) return (false, roleError, null);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? email : request.FullName.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return (false, Describe(created), null);

        var added = await users.AddToRoleAsync(user, role);
        if (!added.Succeeded) return (false, Describe(added), null);

        await audit.WriteAsync("User.Created", "User", user.Id, null,
            new { user.Email, Role = role, user.IsActive });
        return (true, null, user.Id);
    }

    private async Task<(bool Ok, string? Error, string? Id)> UpdateAsync(AdminUserSaveRequest request, string? actorUserId)
    {
        var user = await users.FindByIdAsync(request.Id!);
        if (user is null) return (false, "Không tìm thấy user.", null);

        var currentRoles = (await users.GetRolesAsync(user)).ToList();
        var (role, roleError) = UserRolePolicy.Validate(request.Role, currentRoles);
        if (role is null) return (false, roleError, null);
        List<string> roles = [role];

        var isSelf = user.Id == actorUserId;
        var willBeSuperAdmin = role == AppRoles.SuperAdmin && request.IsActive;
        if (isSelf && !request.IsActive) return (false, "Không thể tự khóa tài khoản của chính mình.", null);
        if (isSelf && currentRoles.Contains(AppRoles.SuperAdmin) && role != AppRoles.SuperAdmin)
            return (false, "Không thể tự gỡ role Super Admin của chính mình.", null);
        if (currentRoles.Contains(AppRoles.SuperAdmin) && user.IsActive && !willBeSuperAdmin
            && !await HasOtherActiveSuperAdminAsync(user.Id))
            return (false, "Phải còn ít nhất một Super Admin đang hoạt động.", null);

        var before = new { Roles = currentRoles, user.IsActive, user.FullName };
        var rolesChanged = !currentRoles.ToHashSet().SetEquals(roles);
        var activeChanged = user.IsActive != request.IsActive;

        user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? user.FullName : request.FullName.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded) return (false, Describe(updated), null);

        if (rolesChanged)
        {
            var removed = await users.RemoveFromRolesAsync(user, currentRoles.Except(roles));
            if (!removed.Succeeded) return (false, Describe(removed), null);
            var added = await users.AddToRolesAsync(user, roles.Except(currentRoles));
            if (!added.Succeeded) return (false, Describe(added), null);
        }

        var passwordChanged = false;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var reset = await users.ResetPasswordAsync(user, token, request.Password);
            if (!reset.Succeeded) return (false, Describe(reset), null);
            passwordChanged = true;
        }

        // Forces existing cookies to re-validate (SecurityStampValidator) so new roles / lockout apply quickly.
        if ((rolesChanged || activeChanged) && !passwordChanged)
            await users.UpdateSecurityStampAsync(user);

        await audit.WriteAsync("User.Updated", "User", user.Id, before,
            new { Role = role, user.IsActive, user.FullName, PasswordReset = passwordChanged });
        return (true, null, user.Id);
    }

    private async Task<bool> HasOtherActiveSuperAdminAsync(string userId)
        => (await users.GetUsersInRoleAsync(AppRoles.SuperAdmin)).Any(u => u.Id != userId && u.IsActive);

    private static IReadOnlyList<string> OrderRoles(IEnumerable<string> roles)
    {
        var order = AppRoles.Assignable.Append(AppRoles.LegacyStaff).ToList();
        return roles.OrderBy(r => order.IndexOf(r) is var i and >= 0 ? i : int.MaxValue).ToList();
    }

    private static string Describe(IdentityResult result) => string.Join("; ", result.Errors.Select(e => e.Description));
}
