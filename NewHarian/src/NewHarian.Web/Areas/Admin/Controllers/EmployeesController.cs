using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Hr;
using NewHarian.Domain.Enums;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class EmployeesController(IAdminEmployeeService employees) : Controller
{
    [HasPermission(Permissions.Employees.View)]
    public async Task<IActionResult> Index(string? q, EmployeeStatus? status, int page = 1, CancellationToken ct = default)
    {
        var (items, pager) = AdminPaging.Apply(await employees.ListAsync(q, status, ct), page);
        ViewBag.Pager = pager;
        ViewBag.Q = q;
        ViewBag.Status = status;
        return View(items);
    }

    [HttpGet]
    [HasPermission(Permissions.Employees.View)]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var model = await employees.GetAsync(id, ct);
        if (model is null) return NotFound();
        return PartialView("_EmployeeForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Employees.Manage)]
    public async Task<IActionResult> Save(EmployeeProfileForm model, CancellationToken ct)
    {
        var (ok, error) = await employees.SaveAsync(
            model, EmployeeEditScope.Hr, User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        if (!ok)
        {
            if (await employees.GetAsync(model.UserId, ct) is { } stored)
                EmployeeProfilePolicy.MergeReadOnly(model, stored, EmployeeEditScope.Hr);
            ModelState.AddModelError(string.Empty, error ?? "Không lưu được.");
            return PartialView("_EmployeeForm", model);
        }
        return Json(new
        {
            ok = true,
            message = "Đã lưu hồ sơ nhân viên.",
            redirect = Url.Action(nameof(Index), new { area = "Admin" })
        });
    }
}
