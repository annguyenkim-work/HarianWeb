using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Abstractions;
using NewHarian.Infrastructure.Persistence;
using NewHarian.Web.Areas.Admin;

namespace NewHarian.Web.Areas.Admin.Controllers;

/// <summary>Hidden Admin-only audit trail — URL /admin/audit-logs (not linked in menu; for maintain/debug).</summary>
[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AuditLogsController(AppDbContext db) : Controller
{
    [HttpGet("/admin/audit-logs")]
    public async Task<IActionResult> Index(
        string? entityType,
        string? action,
        string? q,
        int page = 1,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        const int pageSize = AdminPagerModel.DefaultPageSize;

        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(e => e.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.Action == action);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e =>
                e.EntityId.Contains(term) ||
                (e.UserId != null && e.UserId.Contains(term)) ||
                (e.OldValues != null && e.OldValues.Contains(term)) ||
                (e.NewValues != null && e.NewValues.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditLogRow(
                e.Id,
                e.CreatedAt,
                e.UserId,
                e.Action,
                e.EntityType,
                e.EntityId,
                e.OldValues,
                e.NewValues,
                e.IpAddress))
            .ToListAsync(ct);

        ViewBag.EntityType = entityType;
        ViewBag.Action = action;
        ViewBag.Q = q;
        ViewBag.EntityTypes = await db.AuditLogs.AsNoTracking()
            .Select(e => e.EntityType).Distinct().OrderBy(x => x).ToListAsync(ct);
        ViewBag.Actions = await db.AuditLogs.AsNoTracking()
            .Select(e => e.Action).Distinct().OrderBy(x => x).ToListAsync(ct);
        ViewBag.Pager = new AdminPagerModel
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };

        ViewData["Title"] = "Nhật ký audit";
        return View(items);
    }

    public record AuditLogRow(
        long Id,
        DateTime CreatedAt,
        string? UserId,
        string Action,
        string EntityType,
        string EntityId,
        string? OldValues,
        string? NewValues,
        string? IpAddress);
}
