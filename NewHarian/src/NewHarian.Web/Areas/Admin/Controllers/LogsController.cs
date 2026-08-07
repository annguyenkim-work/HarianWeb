using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Infrastructure.Persistence;
using NewHarian.Web.Areas.Admin;

namespace NewHarian.Web.Areas.Admin.Controllers;

/// <summary>Hidden Admin-only log viewer — URL /admin/logs (not linked in menu).</summary>
[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class LogsController(AppDbContext db) : Controller
{
    [HttpGet("/admin/logs")]
    public async Task<IActionResult> Index(
        string? module,
        LogLevel? level,
        string? q,
        int page = 1,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        const int pageSize = AdminPagerModel.DefaultPageSize;

        var query = db.AppLogEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(e => e.Module == module);
        if (level is { } lv)
            query = query.Where(e => e.Level == (short)lv);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.Message.Contains(term) || (e.Exception != null && e.Exception.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var items = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AppLogRow(
                e.Id,
                e.CreatedAtUtc,
                e.Level,
                e.Module,
                e.Message,
                e.Exception))
            .ToListAsync(ct);

        var modules = await db.AppLogEntries.AsNoTracking()
            .Select(e => e.Module)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync(ct);

        ViewBag.Module = module;
        ViewBag.Level = level;
        ViewBag.Q = q;
        ViewBag.Modules = modules;
        ViewBag.Pager = new AdminPagerModel
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };

        return View(items);
    }

    public record AppLogRow(
        long Id,
        DateTime CreatedAtUtc,
        short Level,
        string Module,
        string Message,
        string? Exception);
}
