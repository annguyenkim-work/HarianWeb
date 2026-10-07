using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Dealers;
using NewHarian.Application.Inventory;
using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class OrdersController(
    IOrderAdminService orders,
    IOrderImportExportService importExport,
    IStatusHistoryService history,
    IDealerService dealers,
    IOrderStockService orderStock) : Controller
{
    private static readonly HashSet<string> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "orderNumber", "customer", "total", "payment", "status", "createdAt", "source"
    };

    private string? ActorUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private string? ActorName() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);

    [HasPermission(Permissions.Orders.View)]
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

        var (items, total) = await orders.AdminListAsync(status, payment, q, sort, dir, from, to, source, page, AdminPagerModel.DefaultPageSize, ct);
        ViewBag.Pager = AdminPaging.Create(total, page);
        return View(items);
    }

    [HttpGet]
    [HasPermission(Permissions.Orders.Export)]
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

        var bytes = await importExport.ExportOrdersExcelAsync(status, payment, q, sort, dir, from, to, source, ct);
        var fileName = $"orders-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet]
    [HasPermission(Permissions.Orders.View)]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var item = await orders.AdminGetAsync(id, ct);
        if (item is null) return NotFound();
        ViewBag.Histories = await history.ListForOrderAsync(id, ct);
        ViewBag.StockPick = await LoadStockPickAsync(id, item.Status, ct);
        return PartialView("_DetailModal", item);
    }

    /// <summary>
    /// Hidden for Cancelled/Refunded. Non-deducted FEFO preview needs PreviewStockPick; actual allocations of a deducted
    /// order need ViewAllocations.
    /// </summary>
    private async Task<OrderStockPickPlanDto?> LoadStockPickAsync(int id, OrderStatus status, CancellationToken ct)
    {
        if (!OrderStatusPolicy.NeedsStockPick(status))
            return null;

        var canPreview = User.HasPermission(Permissions.Orders.PreviewStockPick);
        var canViewAllocations = User.HasPermission(Permissions.Orders.ViewAllocations);

        if (status is OrderStatus.Confirmed)
            return canPreview ? await orderStock.PreviewPickPlanAsync(id, ct) : null;

        var mayBeDeducted = OrderStatusPolicy.RequiresStockDeduction(status);
        if (!canPreview && !(canViewAllocations && mayBeDeducted))
            return null;

        var plan = await orderStock.GetAllocationsAsync(id, ct);
        if (plan is null) return null;
        return (plan.AlreadyDeducted ? canViewAllocations : canPreview) ? plan : null;
    }

    [HttpGet]
    [HasPermission(Permissions.Orders.Print)]
    public async Task<IActionResult> Print(int id, CancellationToken ct)
    {
        var item = await orders.AdminGetAsync(id, ct);
        if (item is null) return NotFound();
        return View(item);
    }

    [HttpGet]
    [HasPermission(Permissions.Orders.Create)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Dealers = await dealers.ListApprovedOptionsAsync(ct);
        return PartialView("_ManualOrderForm", new ManualOrderCreateRequest());
    }

    /// <summary>FEFO gợi ý vị trí/lô theo SKU trên form Thêm đơn (trước khi lưu).</summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [HasPermission(Permissions.Orders.PreviewStockPick)]
    public async Task<IActionResult> PreviewStockPick([FromBody] List<StockPickSkuLineRequest>? lines, CancellationToken ct)
    {
        var plan = await orderStock.PreviewPickBySkusAsync(lines ?? [], ct);
        return Json(plan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Orders.Create)]
    public async Task<IActionResult> Create(ManualOrderCreateRequest model, CancellationToken ct)
    {
        var (ok, error, orderNumber) = await orders.CreateManualOrderAsync(model, ActorUserId(), ActorName(), ct);
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
            message = $"Đã tạo đơn {orderNumber}.",
            redirect = Url.Action(nameof(Index), new { area = "Admin", q = orderNumber })
        });
    }

    [HttpGet]
    [HasPermission(Permissions.Orders.Import)]
    public IActionResult Import()
    {
        return PartialView("_ImportOrdersForm");
    }

    [HttpGet]
    [HasPermission(Permissions.Orders.Import)]
    public IActionResult ImportTemplate()
    {
        var bytes = importExport.BuildOrderImportTemplate();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "orders-import-template.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [HasPermission(Permissions.Orders.Import)]
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
        var result = await importExport.ImportOrdersAsync(stream, ActorUserId(), ActorName(), ct);
        return PartialView("_ImportOrdersResult", result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Orders.ConfirmCod)]
    public async Task<IActionResult> ConfirmCod(int id, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await orders.ConfirmCodAsync(id, internalNotes, ct);
        return ok ? Json(new { ok = true, message = "Đã xác nhận COD." }) : this.JsonFail(error);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Orders.ConfirmBankTransfer)]
    public async Task<IActionResult> ConfirmPayment(int id, string? internalNotes, CancellationToken ct)
    {
        var (ok, error) = await orders.ConfirmBankTransferAsync(id, internalNotes, ct);
        return ok ? Json(new { ok = true, message = "Đã xác nhận chuyển khoản." }) : this.JsonFail(error);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Orders.UpdateStatus)]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? internalNotes, CancellationToken ct)
    {
        if (status == OrderStatus.Cancelled && !User.HasPermission(Permissions.Orders.Cancel))
            return this.JsonFail("Bạn không có quyền hủy đơn.");

        var (ok, error) = await orders.AdminUpdateStatusAsync(id, status, internalNotes, ActorUserId(), ActorName(), ct);
        return ok
            ? Json(new { ok = true, status = status.ToString(), message = "Đã cập nhật trạng thái đơn hàng." })
            : this.JsonFail(error);
    }
}
