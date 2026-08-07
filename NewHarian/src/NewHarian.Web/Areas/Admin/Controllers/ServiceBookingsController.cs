using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public class ServiceBookingsController(IServiceBookingService bookings, IStatusHistoryService history) : Controller
{
    private static readonly HashSet<string> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "customer", "phone", "product", "preferredDate", "status", "createdAt"
    };

    public async Task<IActionResult> Index(
        ServiceBookingStatus? status,
        string? q,
        string? sort,
        string? dir,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        CancellationToken ct = default)
    {
        sort = AdminListQuery.NormalizeSort(sort, SortKeys, "createdAt");
        dir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sort));
        (from, to) = AdminListQuery.NormalizeDateRange(from, to);

        ViewBag.Status = status;
        ViewBag.Q = q;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.From = from;
        ViewBag.To = to;

        var (items, pager) = AdminPaging.Apply(
            await bookings.ListAsync(status, q, sort, dir, from, to, ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var item = await bookings.GetAsync(id, ct);
        if (item is null) return NotFound();
        ViewBag.Histories = await history.ListForBookingAsync(id, ct);
        return PartialView("_DetailModal", item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, ServiceBookingStatus status, string? internalNotes, CancellationToken ct)
    {
        var ok = await bookings.UpdateStatusAsync(id, status, internalNotes, ct);
        if (Request.Headers.XRequestedWith == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            return Json(new { ok, error = ok ? null : "Không tìm thấy.", status = status.ToString() });

        TempData[ok ? "Success" : "Error"] = ok ? "Đã cập nhật." : "Không tìm thấy.";
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
