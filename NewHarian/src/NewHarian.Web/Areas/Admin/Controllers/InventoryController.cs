using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Catalog;
using NewHarian.Application.Inventory;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class InventoryController(IInventoryService inventory, IAdminProductService products) : Controller
{
    [HasPermission(Permissions.Inventory.View)]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        ViewBag.Summary = await inventory.GetSummaryAsync(ct);
        var items = await inventory.ListLocationsAsync(ct);
        return View(items);
    }

    [HasPermission(Permissions.Inventory.View)]
    public IActionResult Locations() => RedirectToAction(nameof(Index));

    [HttpGet]
    [HasPermission(Permissions.Inventory.ViewLots)]
    public async Task<IActionResult> LotsByLocation(int id, CancellationToken ct)
    {
        var lots = await inventory.ListLotsByLocationAsync(id, ct);
        return PartialView("_LocationLots", lots);
    }

    [HttpGet]
    [HasPermission(Permissions.Inventory.ManageLocations)]
    public async Task<IActionResult> EditLocation(int? id, CancellationToken ct)
    {
        if (id is null)
            return PartialView("_LocationForm", new WarehouseLocationSaveRequest());

        var model = await inventory.GetLocationForEditAsync(id.Value, ct);
        if (model is null) return NotFound();
        return PartialView("_LocationForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Inventory.ManageLocations)]
    public async Task<IActionResult> SaveLocation(WarehouseLocationSaveRequest model, CancellationToken ct)
    {
        var (ok, error) = await inventory.SaveLocationAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không lưu được.");
            return PartialView("_LocationForm", model);
        }
        return Json(new
        {
            ok = true,
            message = model.Id is null ? "Đã thêm vị trí kho." : "Đã cập nhật vị trí kho.",
            redirect = Url.Action("Index", "InventorySettings", new { area = "Admin" })
        });
    }

    [HttpGet]
    [HasPermission(Permissions.Inventory.Receive)]
    public async Task<IActionResult> Receive(int? locationId, CancellationToken ct)
    {
        ViewBag.Locations = await inventory.ListLocationsAsync(ct);
        return PartialView("_ReceiveLotForm", new StockLotReceiveRequest
        {
            WarehouseLocationId = locationId ?? 0,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddMonths(12),
            Quantity = 1,
            UnitCost = 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Inventory.Receive)]
    public async Task<IActionResult> Receive(StockLotReceiveRequest model, CancellationToken ct)
    {
        var (ok, error, _) = await inventory.ReceiveLotAsync(model, ActorUserId(), ActorName(), ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không nhập được lô.");
            ViewBag.Locations = await inventory.ListLocationsAsync(ct);
            if (model.ProductVariantId > 0)
                ViewBag.SelectedVariantLabel = await products.GetVariantDisplayAsync(model.ProductVariantId, ct);
            return PartialView("_ReceiveLotForm", model);
        }
        return Json(new { ok = true, message = "Đã nhập lô hàng.", redirect = Url.Action(nameof(Index)) });
    }

    [HttpGet]
    [HasPermission(Permissions.Inventory.Adjust)]
    public async Task<IActionResult> Adjust(int id, CancellationToken ct)
    {
        var lot = await inventory.GetLotAsync(id, ct);
        if (lot is null) return NotFound();
        ViewBag.Lot = lot;
        return PartialView("_AdjustLotForm", new StockLotAdjustRequest
        {
            LotId = lot.Id,
            QuantityOnHand = lot.QuantityOnHand
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Inventory.Adjust)]
    public async Task<IActionResult> Adjust(StockLotAdjustRequest model, CancellationToken ct)
    {
        var (ok, error) = await inventory.AdjustLotAsync(model, ActorUserId(), ActorName(), ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không điều chỉnh được.");
            ViewBag.Lot = await inventory.GetLotAsync(model.LotId, ct);
            return PartialView("_AdjustLotForm", model);
        }
        return Json(new { ok = true, message = "Đã điều chỉnh tồn kho.", redirect = Url.Action(nameof(Index)) });
    }

    [HasPermission(Permissions.Inventory.ViewHistory)]
    public async Task<IActionResult> History(
        StockHistoryFilterKind? kind,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        CancellationToken ct = default)
    {
        kind ??= StockHistoryFilterKind.All;
        (from, to) = AdminListQuery.NormalizeDateRange(from, to);
        ViewBag.Kind = kind;
        ViewBag.From = from;
        ViewBag.To = to;

        var (items, total) = await inventory.ListMovementsAsync(kind.Value, from, to, page, AdminPagerModel.DefaultPageSize, ct);
        ViewBag.Pager = AdminPaging.Create(total, page);
        return View(items);
    }

    private string? ActorUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private string? ActorName() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email);
}
