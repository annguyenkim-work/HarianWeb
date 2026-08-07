using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public class OrdersController(IOrderService orders, IStatusHistoryService history) : Controller
{
    private static readonly HashSet<string> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "orderNumber", "customer", "total", "payment", "status", "createdAt"
    };

    public async Task<IActionResult> Index(
        OrderStatus? status,
        PaymentMethod? payment,
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
        ViewBag.Payment = payment;
        ViewBag.Q = q;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.From = from;
        ViewBag.To = to;

        var (items, pager) = AdminPaging.Apply(
            await orders.AdminListAsync(status, payment, q, sort, dir, from, to, ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var item = await orders.AdminGetAsync(id, ct);
        if (item is null) return NotFound();
        ViewBag.Histories = await history.ListForOrderAsync(id, ct);
        return PartialView("_DetailModal", item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCod(int id, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await orders.ConfirmCodAsync(id, internalNotes, ct);
        return Json(new { ok, error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int id, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await orders.ConfirmBankTransferAsync(id, internalNotes, ct);
        return Json(new { ok, error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await orders.AdminUpdateStatusAsync(id, status, internalNotes, ct);
        return Json(new { ok, error, status = status.ToString() });
    }
}
