using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Dealers;
using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public class OrdersController(IOrderService orders, IStatusHistoryService history, IDealerService dealers) : Controller
{
    private static readonly HashSet<string> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "orderNumber", "customer", "total", "payment", "status", "createdAt", "source"
    };

    public async Task<IActionResult> Index(
        OrderStatus? status,
        PaymentMethod? payment,
        OrderSource? source,
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
        ViewBag.Source = source;
        ViewBag.Q = q;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.From = from;
        ViewBag.To = to;

        var (items, pager) = AdminPaging.Apply(
            await orders.AdminListAsync(status, payment, q, sort, dir, from, to, source, ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        OrderStatus? status,
        PaymentMethod? payment,
        OrderSource? source,
        string? q,
        string? sort,
        string? dir,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        sort = AdminListQuery.NormalizeSort(sort, SortKeys, "createdAt");
        dir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sort));
        (from, to) = AdminListQuery.NormalizeDateRange(from, to);

        var bytes = await orders.ExportOrdersExcelAsync(status, payment, q, sort, dir, from, to, source, ct);
        var fileName = $"orders-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var item = await orders.AdminGetAsync(id, ct);
        if (item is null) return NotFound();
        ViewBag.Histories = await history.ListForOrderAsync(id, ct);
        return PartialView("_DetailModal", item);
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id, CancellationToken ct)
    {
        var item = await orders.AdminGetAsync(id, ct);
        if (item is null) return NotFound();
        return View(item);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Dealers = await dealers.ListApprovedOptionsAsync(ct);
        return PartialView("_ManualOrderForm", new ManualOrderCreateRequest());
    }

    [HttpGet]
    public async Task<IActionResult> SuggestVariants(string? q, CancellationToken ct)
    {
        var items = await orders.SuggestVariantsAsync(q, 15, ct);
        return Json(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ManualOrderCreateRequest model, CancellationToken ct)
    {
        var (ok, error, orderNumber) = await orders.CreateManualOrderAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không tạo được đơn.");
            ViewBag.Dealers = await dealers.ListApprovedOptionsAsync(ct);
            return PartialView("_ManualOrderForm", model);
        }

        return Json(new
        {
            ok = true,
            orderNumber,
            redirect = Url.Action(nameof(Index), new { area = "Admin", q = orderNumber })
        });
    }

    [HttpGet]
    public IActionResult Import()
    {
        return PartialView("_ImportOrdersForm");
    }

    [HttpGet]
    public IActionResult ImportTemplate()
    {
        var bytes = orders.BuildOrderImportTemplate();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "orders-import-template.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ViewBag.Error = "Vui lòng chọn file Excel (.xlsx).";
            return PartialView("_ImportOrdersForm");
        }

        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.Error = "Chỉ hỗ trợ file .xlsx.";
            return PartialView("_ImportOrdersForm");
        }

        await using var stream = file.OpenReadStream();
        var result = await orders.ImportOrdersAsync(stream, ct);
        return PartialView("_ImportOrdersResult", result);
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
