using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;
using System.Globalization;

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

        var (items, total) = await bookings.ListAsync(status, q, sort, dir, from, to, page, AdminPagerModel.DefaultPageSize, ct);
        ViewBag.Pager = AdminPaging.Create(total, page);
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
    public async Task<IActionResult> UpdateStatus(int id, ServiceBookingStatus status, string? internalNotes, string? citizenId, string? amount, CancellationToken ct)
    {
        decimal? parsedAmount = null;
        if (!string.IsNullOrWhiteSpace(amount))
        {
            if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
                && !decimal.TryParse(amount, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out v))
                return Json(new { ok = false, error = "Thành tiền không hợp lệ." });
            parsedAmount = v;
        }

        var (ok, error) = await bookings.UpdateStatusAsync(id, status, internalNotes, citizenId, parsedAmount, ct);
        return Json(new { ok, error, status = status.ToString() });
    }
}
