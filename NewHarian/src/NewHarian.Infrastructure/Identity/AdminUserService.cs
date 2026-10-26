using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Identity;

public sealed class AdminUserService(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    IAuditService audit,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    public async Task<IReadOnlyList<AdminUserListItemDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        var links = await db.EmployeeProfiles.AsNoTracking()
            .Where(p => p.UserId != null)
            .Select(p => new { p.UserId, p.Id, p.EmployeeCode })
            .ToDictionaryAsync(x => x.UserId!, ct);

        var rows = new List<AdminUserListItemDto>(list.Count);
        foreach (var u in list)
        {
            var roles = await users.GetRolesAsync(u);
            links.TryGetValue(u.Id, out var link);
            rows.Add(new AdminUserListItemDto(
                u.Id, u.Email ?? "", u.FullName, u.IsActive, OrderRoles(roles), u.CreatedAt,
                link?.EmployeeCode, link?.Id));
        }
        return rows;
    }

    public async Task<AdminUserSaveRequest?> GetForEditAsync(string id, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return null;
        var roles = OrderRoles(await users.GetRolesAsync(user));
        var profile = await db.EmployeeProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == id, ct);
        return new AdminUserSaveRequest
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FullName = profile?.FullName ?? user.FullName,
            IsActive = user.IsActive,
            Role = UserRolePolicy.PreselectedRole(roles),
            CurrentRoles = roles.ToList(),
            EmployeeProfileId = profile?.Id
        };
    }

    public async Task<(bool Ok, string? Error, string? Id)> SaveAsync(
        AdminUserSaveRequest request, string? actorUserId, CancellationToken ct = default)
    {
        logger.LogInformation("SaveUser Start Id={Id} Email={Email}", request.Id, request.Email);
        try
        {
            var (ok, error, id) = request.Id is null
                ? await CreateAsync(request, ct)
                : await UpdateAsync(request, actorUserId, ct);

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

    private async Task<(bool Ok, string? Error, string? Id)> CreateAsync(AdminUserSaveRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email)) return (false, "Email bắt buộc.", null);
        if (string.IsNullOrWhiteSpace(request.Password)) return (false, "Mật khẩu bắt buộc khi tạo user.", null);
        if (await users.FindByEmailAsync(email) is not null) return (false, "Email đã được dùng cho user khác.", null);

        var (role, roleError) = UserRolePolicy.Validate(request.Role, currentRoles: []);
        if (role is null) return (false, roleError, null);

        string fullName;
        if (request.EmployeeProfileId is int profileId)
        {
            var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.Id == profileId, ct);
            if (profile is null) return (false, "Không tìm thấy nhân viên để gắn.", null);
            if (!string.IsNullOrEmpty(profile.UserId)) return (false, "Nhân viên đã được gắn tài khoản khác.", null);
            fullName = profile.FullName;
        }
        else
            fullName = string.IsNullOrWhiteSpace(request.FullName) ? email : request.FullName.Trim();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return (false, Describe(created), null);

        var added = await users.AddToRoleAsync(user, role);
        if (!added.Succeeded) return (false, Describe(added), null);

        if (request.EmployeeProfileId is int linkId)
        {
            var linkError = await LinkProfileAsync(linkId, user.Id, fullName, ct);
            if (linkError is not null) return (false, linkError, null);
        }

        await audit.WriteAsync("User.Created", "User", user.Id, null,
            new { user.Email, Role = role, user.IsActive, request.EmployeeProfileId });
        return (true, null, user.Id);
    }

    private async Task<(bool Ok, string? Error, string? Id)> UpdateAsync(
        AdminUserSaveRequest request, string? actorUserId, CancellationToken ct)
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

        var currentLink = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (request.EmployeeProfileId is int newProfileId)
        {
            if (currentLink is null || currentLink.Id != newProfileId)
            {
                var target = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.Id == newProfileId, ct);
                if (target is null) return (false, "Không tìm thấy nhân viên để gắn.", null);
                if (!string.IsNullOrEmpty(target.UserId) && target.UserId != user.Id)
                    return (false, "Nhân viên đã được gắn tài khoản khác.", null);
                if (currentLink is not null)
                {
                    currentLink.UserId = null;
                    currentLink.UpdatedAt = DateTime.UtcNow;
                }
                target.UserId = user.Id;
                target.UpdatedAt = DateTime.UtcNow;
                user.FullName = target.FullName;
            }
            else
                user.FullName = currentLink.FullName;
        }
        else if (currentLink is not null)
        {
            // Keep existing link; allow FullName override only when no profile.
            user.FullName = currentLink.FullName;
        }
        else if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        var before = new { Roles = currentRoles, user.IsActive, user.FullName, EmployeeProfileId = currentLink?.Id };
        var rolesChanged = !currentRoles.ToHashSet().SetEquals(roles);
        var activeChanged = user.IsActive != request.IsActive;

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

        if ((rolesChanged || activeChanged) && !passwordChanged)
            await users.UpdateSecurityStampAsync(user);

        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("User.Updated", "User", user.Id, before,
            new { Role = role, user.IsActive, user.FullName, PasswordReset = passwordChanged, request.EmployeeProfileId });
        return (true, null, user.Id);
    }

    private async Task<string?> LinkProfileAsync(int profileId, string userId, string fullName, CancellationToken ct)
    {
        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.Id == profileId, ct);
        if (profile is null) return "Không tìm thấy nhân viên để gắn.";
        if (!string.IsNullOrEmpty(profile.UserId)) return "Nhân viên đã được gắn tài khoản khác.";
        profile.UserId = userId;
        profile.FullName = string.IsNullOrWhiteSpace(fullName) ? profile.FullName : fullName;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
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
