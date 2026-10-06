using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Catalog;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class ColorsController(IAdminColorService colors) : Controller
{
    [HasPermission(Permissions.Colors.View)]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var mapped = await colors.ListAsync(ct);
        var (items, pager) = AdminPaging.Apply(mapped, page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    [HasPermission(Permissions.Colors.Edit)]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
            return PartialView("_ColorForm", new ColorDefinitionSaveRequest());

        var model = await colors.GetForEditAsync(id.Value, ct);
        if (model is null) return NotFound();
        return PartialView("_ColorForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Colors.Edit)]
    public async Task<IActionResult> Save(ColorDefinitionSaveRequest model, CancellationToken ct)
    {
        var (ok, error, _) = await colors.SaveAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Lỗi lưu.");
            return PartialView("_ColorForm", model);
        }
        return Json(new { ok = true, redirect = Url.Action(nameof(Index), new { area = "Admin" }) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Colors.Edit)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (ok, error) = await colors.DeleteAsync(id, ct);
        if (!ok)
            TempData["Error"] = error;
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
