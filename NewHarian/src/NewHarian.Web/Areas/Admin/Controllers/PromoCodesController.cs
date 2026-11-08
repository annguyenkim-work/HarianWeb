using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.PromoCodes;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class PromoCodesController(IAdminPromoCodeService promoCodes) : Controller
{
    [HasPermission(Permissions.PromoCodes.Manage)]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var mapped = await promoCodes.ListAsync(ct);
        var (items, pager) = AdminPaging.Apply(mapped, page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    [HasPermission(Permissions.PromoCodes.Manage)]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
            return PartialView("_PromoCodeForm", await promoCodes.NewAsync());

        var model = await promoCodes.GetForEditAsync(id.Value, ct);
        if (model is null) return NotFound();
        return PartialView("_PromoCodeForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.PromoCodes.Manage)]
    public async Task<IActionResult> Save(PromoCodeSaveRequest model, CancellationToken ct)
    {
        var (ok, error, _) = await promoCodes.SaveAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Lỗi lưu.");
            return PartialView("_PromoCodeForm", model);
        }
        return Json(new
        {
            ok = true,
            message = model.Id is null ? "Đã thêm mã giảm giá." : "Đã cập nhật mã giảm giá.",
            redirect = Url.Action(nameof(Index), new { area = "Admin" })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.PromoCodes.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (ok, error) = await promoCodes.DeleteAsync(id, ct);
        this.FlashResult(ok, "Đã xóa mã giảm giá.", error);
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
