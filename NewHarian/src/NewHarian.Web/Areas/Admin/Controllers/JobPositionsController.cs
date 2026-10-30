using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Hr;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class JobPositionsController(IAdminJobPositionService positions) : Controller
{
    [HasPermission(Permissions.Employees.Manage)]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var (items, pager) = AdminPaging.Apply(await positions.ListAsync(ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    [HasPermission(Permissions.Employees.Manage)]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null)
            return PartialView("_JobPositionForm", new JobPositionForm());

        var model = await positions.GetAsync(id.Value, ct);
        if (model is null) return NotFound();
        return PartialView("_JobPositionForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Employees.Manage)]
    public async Task<IActionResult> Save(JobPositionForm model, CancellationToken ct)
    {
        var (ok, error, _) = await positions.SaveAsync(model, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không lưu được.");
            return PartialView("_JobPositionForm", model);
        }

        return Json(new
        {
            ok = true,
            message = model.Id is null ? "Đã thêm vị trí công việc." : "Đã cập nhật vị trí công việc.",
            redirect = Url.Action(nameof(Index), new { area = "Admin" })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Employees.Manage)]
    public async Task<IActionResult> Move(int id, int direction, CancellationToken ct)
    {
        await positions.MoveAsync(id, direction, ct);
        this.FlashSuccess("Đã đổi thứ tự vị trí công việc.");
        return AdminListRedirect.ToRefererOrIndex(this);
    }
}
