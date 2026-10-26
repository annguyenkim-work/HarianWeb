using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Hr;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Hr;

public sealed class AdminEmployeeService(
    AppDbContext db,
    IAdminUserService userAdmin,
    IAuditService audit,
    ILogger<AdminEmployeeService> logger) : IAdminEmployeeService
{
    public async Task<IReadOnlyList<EmployeeListItemDto>> ListAsync(
        string? q, EmployeeStatus? status, CancellationToken ct = default)
    {
        var internalRoles = RolePermissionMap.InternalRoles.ToList();
        var userRoles = await (
                from ur in db.UserRoles
                join r in db.Roles on ur.RoleId equals r.Id
                where internalRoles.Contains(r.Name!)
                select new { ur.UserId, Role = r.Name! })
            .AsNoTracking()
            .ToListAsync(ct);
        var rolesByUser = userRoles.GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)OrderRoles(g.Select(x => x.Role)));

        var profiles = await db.EmployeeProfiles.AsNoTracking().ToListAsync(ct);
        var linkedUserIds = profiles.Where(p => p.UserId is not null).Select(p => p.UserId!).ToHashSet();
        var userIdsNeeded = linkedUserIds.Union(rolesByUser.Keys).ToList();
        var userMap = await db.Users.AsNoTracking()
            .Where(u => userIdsNeeded.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var rows = new List<EmployeeListItemDto>();

        foreach (var p in profiles)
        {
            userMap.TryGetValue(p.UserId ?? "", out var u);
            var roles = p.UserId is not null && rolesByUser.TryGetValue(p.UserId, out var r) ? r : Array.Empty<string>();
            rows.Add(new EmployeeListItemDto(
                p.Id,
                p.UserId,
                u?.Email ?? "",
                p.FullName,
                roles,
                u?.IsActive ?? true,
                p.EmployeeCode,
                p.Status,
                EmployeeProfilePolicy.IsPersonalComplete(p),
                p.UserId is not null));
        }

        // Legacy: internal-role users still without a profile.
        foreach (var userId in rolesByUser.Keys.Where(id => !linkedUserIds.Contains(id)))
        {
            if (!userMap.TryGetValue(userId, out var u)) continue;
            rows.Add(new EmployeeListItemDto(
                null,
                u.Id,
                u.Email ?? "",
                u.FullName,
                rolesByUser[userId],
                u.IsActive,
                null,
                null,
                false,
                true));
        }

        var term = q?.Trim();
        return rows
            .Where(x => status is null || x.Status == status)
            .Where(x => string.IsNullOrEmpty(term) || Matches(x, term))
            .OrderBy(x => x.EmployeeCode is null)
            .ThenBy(x => x.EmployeeCode, StringComparer.Ordinal)
            .ThenBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<EmployeeProfileForm?> GetByIdAsync(int id, CancellationToken ct = default)
        => LoadFormAsync(profileId: id, userId: null, ct);

    public Task<EmployeeProfileForm?> GetByUserIdAsync(string userId, CancellationToken ct = default)
        => LoadFormAsync(profileId: null, userId: userId, ct);

    public async Task<(bool Ok, string? Error, int? Id)> SaveAsync(
        EmployeeProfileForm form, EmployeeEditScope scope, string? actorUserId, CancellationToken ct = default)
    {
        logger.LogInformation("SaveEmployeeProfile Start Id={Id} UserId={UserId} Scope={Scope}", form.Id, form.UserId, scope);
        try
        {
            if (EmployeeProfilePolicy.Validate(form, scope, DateOnly.FromDateTime(DateTime.Today)) is { } error)
                return Reject(form, error);

            EmployeeProfile profile;
            ApplicationUser? user = null;
            var isNew = form.Id is null;
            object? before = null;

            if (form.Id is int id)
            {
                profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.Id == id, ct)
                    ?? throw new InvalidOperationException("Profile not found");
                if (!string.IsNullOrEmpty(profile.UserId))
                    user = await db.Users.FirstOrDefaultAsync(u => u.Id == profile.UserId, ct);
                before = Snapshot(profile);
            }
            else if (!string.IsNullOrWhiteSpace(form.UserId))
            {
                // Legacy first save: user exists, profile does not.
                user = await db.Users.FirstOrDefaultAsync(u => u.Id == form.UserId, ct);
                if (user is null) return Reject(form, "Không tìm thấy nhân viên.");
                if (await db.EmployeeProfiles.AnyAsync(p => p.UserId == user.Id, ct))
                    return Reject(form, "Hồ sơ vừa được người khác cập nhật. Vui lòng tải lại trang và thử lại.");

                var codes = await db.EmployeeProfiles.Select(p => p.EmployeeCode).ToListAsync(ct);
                profile = new EmployeeProfile
                {
                    UserId = user.Id,
                    FullName = form.FullName.Trim(),
                    EmployeeCode = EmployeeCodeGenerator.Allocate(form.FullName, codes),
                    CreatedAt = DateTime.UtcNow
                };
                db.EmployeeProfiles.Add(profile);
                isNew = true;
            }
            else if (scope == EmployeeEditScope.Hr)
            {
                var codes = await db.EmployeeProfiles.Select(p => p.EmployeeCode).ToListAsync(ct);
                profile = new EmployeeProfile
                {
                    FullName = form.FullName.Trim(),
                    EmployeeCode = EmployeeCodeGenerator.Allocate(form.FullName, codes),
                    CreatedAt = DateTime.UtcNow
                };
                db.EmployeeProfiles.Add(profile);
                isNew = true;
            }
            else
                return Reject(form, "Không tìm thấy nhân viên.");

            if (scope == EmployeeEditScope.Hr || isNew)
                profile.FullName = form.FullName.Trim();
            else if (user is not null)
                profile.FullName = form.FullName.Trim();

            EmployeeProfilePolicy.Apply(profile, form, scope);
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedByUserId = actorUserId;

            if (user is not null)
            {
                user.FullName = profile.FullName;
                user.UpdatedAt = DateTime.UtcNow;
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "SaveEmployeeProfile Done rejected Id={Id} Error={Error}", form.Id, "Conflict");
                return (false, "Hồ sơ vừa được người khác cập nhật. Vui lòng tải lại trang và thử lại.", null);
            }

            await audit.WriteAsync(isNew ? "Employee.Created" : "Employee.Updated", "EmployeeProfile",
                profile.Id.ToString(), before, Snapshot(profile), ct);
            logger.LogInformation("SaveEmployeeProfile Done Id={Id} Code={Code} Scope={Scope}",
                profile.Id, profile.EmployeeCode, scope);
            return (true, null, profile.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveEmployeeProfile Error Id={Id}", form.Id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> GrantAccountAsync(
        GrantEmployeeAccountRequest request, string? actorUserId, CancellationToken ct = default)
    {
        logger.LogInformation("GrantEmployeeAccount Start ProfileId={ProfileId} Email={Email}", request.ProfileId, request.Email);
        try
        {
            var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.Id == request.ProfileId, ct);
            if (profile is null) return GrantReject(request, "Không tìm thấy nhân viên.");
            if (!string.IsNullOrEmpty(profile.UserId)) return GrantReject(request, "Nhân viên đã có tài khoản.");
            var email = profile.PersonalEmail?.Trim();
            if (string.IsNullOrWhiteSpace(email))
                return GrantReject(request, "Nhân viên chưa có email cá nhân. Cập nhật Email cá nhân trên hồ sơ trước khi cấp tài khoản.");

            var save = new AdminUserSaveRequest
            {
                Email = email,
                Password = request.Password,
                FullName = profile.FullName,
                Role = request.Role,
                IsActive = request.IsActive,
                EmployeeProfileId = profile.Id
            };
            var (ok, error, _) = await userAdmin.SaveAsync(save, actorUserId, ct);
            if (!ok) return GrantReject(request, error ?? "Không tạo được tài khoản.");

            logger.LogInformation("GrantEmployeeAccount Done ProfileId={ProfileId}", request.ProfileId);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GrantEmployeeAccount Error ProfileId={ProfileId}", request.ProfileId);
            throw;
        }
    }

    public async Task<IReadOnlyList<EmployeeLinkOptionDto>> ListLinkOptionsAsync(
        int? includeProfileId, CancellationToken ct = default)
    {
        return await db.EmployeeProfiles.AsNoTracking()
            .Where(p => p.UserId == null || p.Id == includeProfileId)
            .OrderBy(p => p.EmployeeCode)
            .Select(p => new EmployeeLinkOptionDto(p.Id, p.EmployeeCode, p.FullName, p.PersonalEmail))
            .ToListAsync(ct);
    }

    private async Task<EmployeeProfileForm?> LoadFormAsync(int? profileId, string? userId, CancellationToken ct)
    {
        EmployeeProfile? p = null;
        ApplicationUser? user = null;

        if (profileId is int pid)
        {
            p = await db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct);
            if (p is null) return null;
            if (!string.IsNullOrEmpty(p.UserId))
                user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == p.UserId, ct);
        }
        else if (!string.IsNullOrWhiteSpace(userId))
        {
            user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null) return null;
            p = await db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        }
        else return null;

        var roles = Array.Empty<string>();
        if (user is not null)
        {
            roles = (await (
                    from ur in db.UserRoles
                    join r in db.Roles on ur.RoleId equals r.Id
                    where ur.UserId == user.Id
                    select r.Name!)
                .ToListAsync(ct)).ToArray();
            roles = OrderRoles(roles).ToArray();
        }

        if (p is null)
        {
            return new EmployeeProfileForm
            {
                UserId = user!.Id,
                Email = user.Email ?? "",
                FullName = user.FullName,
                Roles = roles.ToList(),
                HasAccount = true
            };
        }

        return ToForm(p, user, roles);
    }

    private static EmployeeProfileForm ToForm(EmployeeProfile p, ApplicationUser? user, IReadOnlyList<string> roles)
        => new()
        {
            Id = p.Id,
            UserId = p.UserId,
            Email = user?.Email ?? "",
            EmployeeCode = p.EmployeeCode,
            FullName = p.FullName,
            Roles = roles.ToList(),
            HasAccount = p.UserId is not null,
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            Phone = p.Phone,
            PersonalEmail = p.PersonalEmail,
            Address = p.Address,
            CitizenId = p.CitizenId,
            EmergencyContactName = p.EmergencyContactName,
            EmergencyContactPhone = p.EmergencyContactPhone,
            HireDate = p.HireDate,
            TerminationDate = p.TerminationDate,
            Status = p.Status,
            HrNotes = p.HrNotes
        };

    private (bool Ok, string? Error, int? Id) Reject(EmployeeProfileForm form, string error)
    {
        logger.LogWarning("SaveEmployeeProfile Done rejected Id={Id} Error={Error}", form.Id, error);
        return (false, error, null);
    }

    private (bool Ok, string? Error) GrantReject(GrantEmployeeAccountRequest request, string error)
    {
        logger.LogWarning("GrantEmployeeAccount Done rejected ProfileId={ProfileId} Error={Error}", request.ProfileId, error);
        return (false, error);
    }

    private static bool Matches(EmployeeListItemDto row, string term)
        => Contains(row.Email, term) || Contains(row.FullName, term) || Contains(row.EmployeeCode, term)
           || row.Roles.Any(r => Contains(AppRoles.Label(r), term));

    private static bool Contains(string? value, string term)
        => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private static object Snapshot(EmployeeProfile p) => new
    {
        p.EmployeeCode,
        p.FullName,
        p.UserId,
        p.DateOfBirth,
        p.Gender,
        p.Phone,
        p.PersonalEmail,
        HasAddress = !string.IsNullOrWhiteSpace(p.Address),
        HasCitizenId = !string.IsNullOrWhiteSpace(p.CitizenId),
        p.EmergencyContactName,
        p.EmergencyContactPhone,
        p.HireDate,
        p.TerminationDate,
        p.Status,
        p.HrNotes
    };

    private static List<string> OrderRoles(IEnumerable<string> roles)
    {
        var order = AppRoles.Assignable.Append(AppRoles.LegacyStaff).ToList();
        return roles.OrderBy(r => order.IndexOf(r) is var i and >= 0 ? i : int.MaxValue).ToList();
    }
}
