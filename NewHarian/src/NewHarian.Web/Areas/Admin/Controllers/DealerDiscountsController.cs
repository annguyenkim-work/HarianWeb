using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.DealerDiscounts;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class DealerDiscountsController(IAdminDealerDiscountService discounts) : Controller
{
    [HasPermission(Permissions.Dealers.View)]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await discounts.GetOverviewAsync(ct));

    [HttpGet]
    [HasPermission(Permissions.Dealers.ManageDiscounts)]
    public async Task<IActionResult> Edit(int? id, int? productId, CancellationToken ct)
    {
        var model = await discounts.GetGroupEditAsync(id, productId, ct);
        if (id is not null && model.Input.Id is null) return NotFound();
        return PartialView("_GroupForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Dealers.ManageDiscounts)]
    public async Task<IActionResult> Save(DealerDiscountGroupInput input, CancellationToken ct)
    {
        var (ok, error, _) = await discounts.SaveGroupAsync(input, User.Identity?.Name, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không lưu được.");
            var model = await discounts.GetGroupEditAsync(input.Id, null, ct);
            model.Input = input;
            return PartialView("_GroupForm", model);
        }
        return Json(new
        {
            ok = true,
            message = input.Id is null ? "Đã thêm nhóm chiết khấu." : "Đã cập nhật nhóm chiết khấu.",
            redirect = Url.Action(nameof(Index), new { area = "Admin" })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Dealers.ManageDiscounts)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (ok, error) = await discounts.DeleteGroupAsync(id, User.Identity?.Name, ct);
        this.FlashResult(ok, "Đã xóa nhóm chiết khấu.", error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Dealers.ManageDiscounts)]
    public async Task<IActionResult> Move(int id, int direction, CancellationToken ct)
    {
        var (ok, error) = await discounts.MoveGroupAsync(id, direction, ct);
        if (!ok) this.FlashError(error);
        return RedirectToAction(nameof(Index));
    }
}
