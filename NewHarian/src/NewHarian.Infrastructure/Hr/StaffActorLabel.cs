using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Hr;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Hr;

public sealed class StaffActorLabel(AppDbContext db) : IStaffActorLabel
{
    public async Task<string> ResolveAsync(string? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return "Hệ thống";

        var profile = await db.EmployeeProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.EmployeeCode, p.FullName })
            .FirstOrDefaultAsync(ct);
        if (profile is not null && !string.IsNullOrWhiteSpace(profile.EmployeeCode))
            return profile.EmployeeCode;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FullName, u.Email })
            .FirstOrDefaultAsync(ct);
        if (user is null) return userId;
        if (!string.IsNullOrWhiteSpace(user.FullName)) return user.FullName;
        if (!string.IsNullOrWhiteSpace(user.Email)) return user.Email!;
        return userId;
    }
}
