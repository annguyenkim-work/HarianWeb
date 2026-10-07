using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Hr;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Hr;

public sealed class AdminEmployeeService(
    AppDbContext db,
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
        var userIds = rolesByUser.Keys.ToList();

        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToListAsync(ct);
        var profiles = await db.EmployeeProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, ct);

        var term = q?.Trim();
        return users
            .Select(u =>
            {
                var p = profiles.GetValueOrDefault(u.Id);
                return new
                {
                    Profile = p,
                    Row = new EmployeeListItemDto(
                        u.Id, u.Email ?? "", u.FullName, rolesByUser[u.Id], u.IsActive,
                        p?.EmployeeCode, p?.Status,
                        EmployeeProfilePolicy.IsPersonalComplete(p))
                };
            })
            .Where(x => status is null || x.Row.Status == status)
            .Where(x => string.IsNullOrEmpty(term) || Matches(x.Row, x.Profile, term))
            .OrderBy(x => x.Row.EmployeeCode is null)
            .ThenBy(x => x.Row.EmployeeCode, StringComparer.Ordinal)
            .ThenBy(x => x.Row.Email, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Row)
            .ToList();
    }

    public async Task<EmployeeProfileForm?> GetAsync(string userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;

        var roles = await (
                from ur in db.UserRoles
                join r in db.Roles on ur.RoleId equals r.Id
                where ur.UserId == userId
                select r.Name!)
            .ToListAsync(ct);
        var p = await db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);

        var form = new EmployeeProfileForm
        {
            UserId = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName,
            Roles = OrderRoles(roles),
        };
        if (p is null) return form;

        form.EmployeeCode = p.EmployeeCode;
        form.DateOfBirth = p.DateOfBirth;
        form.Gender = p.Gender;
        form.Phone = p.Phone;
        form.PersonalEmail = p.PersonalEmail;
        form.Address = p.Address;
        form.CitizenId = p.CitizenId;
        form.EmergencyContactName = p.EmergencyContactName;
        form.EmergencyContactPhone = p.EmergencyContactPhone;
        form.HireDate = p.HireDate;
        form.TerminationDate = p.TerminationDate;
        form.Status = p.Status;
        form.HrNotes = p.HrNotes;
        return form;
    }

    public async Task<(bool Ok, string? Error)> SaveAsync(
        EmployeeProfileForm form, EmployeeEditScope scope, string? actorUserId, CancellationToken ct = default)
    {
        logger.LogInformation("SaveEmployeeProfile Start UserId={UserId} Scope={Scope}", form.UserId, scope);
        try
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == form.UserId, ct);
            if (user is null) return Reject(form, "Không tìm thấy nhân viên.");

            if (EmployeeProfilePolicy.Validate(form, scope, DateOnly.FromDateTime(DateTime.Today)) is { } error)
                return Reject(form, error);

            var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
            var isNew = profile is null;
            var before = isNew ? null : Snapshot(profile!, user.FullName);
            if (profile is null)
            {
                var codes = await db.EmployeeProfiles.Select(p => p.EmployeeCode).ToListAsync(ct);
                profile = new EmployeeProfile
                {
                    UserId = user.Id,
                    EmployeeCode = EmployeeProfilePolicy.NextCode(codes),
                    CreatedAt = DateTime.UtcNow
                };
                db.EmployeeProfiles.Add(profile);
            }

            EmployeeProfilePolicy.Apply(profile, form, scope);
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedByUserId = actorUserId;
            user.FullName = form.FullName.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // Unique UserId / EmployeeCode lost a race with a concurrent first save.
                logger.LogWarning(ex, "SaveEmployeeProfile Done rejected UserId={UserId} Error={Error}", form.UserId, "Conflict");
                return (false, "Hồ sơ vừa được người khác cập nhật. Vui lòng tải lại trang và thử lại.");
            }

            await audit.WriteAsync(isNew ? "Employee.Created" : "Employee.Updated", "EmployeeProfile",
                profile.Id.ToString(), before, Snapshot(profile, user.FullName), ct);
            logger.LogInformation("SaveEmployeeProfile Done UserId={UserId} Code={Code} Scope={Scope}",
                user.Id, profile.EmployeeCode, scope);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveEmployeeProfile Error UserId={UserId}", form.UserId);
            throw;
        }
    }

    private (bool Ok, string? Error) Reject(EmployeeProfileForm form, string error)
    {
        logger.LogWarning("SaveEmployeeProfile Done rejected UserId={UserId} Error={Error}", form.UserId, error);
        return (false, error);
    }

    private static bool Matches(EmployeeListItemDto row, EmployeeProfile? p, string term)
        => Contains(row.Email, term) || Contains(row.FullName, term) || Contains(row.EmployeeCode, term)
           || Contains(p?.Phone, term) || row.Roles.Any(r => Contains(AppRoles.Label(r), term));

    private static bool Contains(string? value, string term)
        => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    // ID number and address stay out of the audit trail; only whether they are filled in is recorded.
    private static object Snapshot(EmployeeProfile p, string fullName) => new
    {
        p.EmployeeCode,
        FullName = fullName,
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
