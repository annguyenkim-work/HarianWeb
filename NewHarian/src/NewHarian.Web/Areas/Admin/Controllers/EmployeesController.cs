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
    [HasPermission(Permissions.Employees.Manage)]
    public IActionResult Create()
        => PartialView("_EmployeeForm", new EmployeeProfileForm { Status = EmployeeStatus.Active });

    [HttpGet]
    [HasPermission(Permissions.Employees.View)]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        EmployeeProfileForm? model;
        if (id.StartsWith("u:", StringComparison.Ordinal))
            model = await employees.GetByUserIdAsync(id[2..], ct);
        else if (int.TryParse(id, out var profileId))
            model = await employees.GetByIdAsync(profileId, ct);
        else
            model = await employees.GetByUserIdAsync(id, ct);

        if (model is null) return NotFound();
        return PartialView("_EmployeeForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Employees.Manage)]
    public async Task<IActionResult> Save(EmployeeProfileForm model, CancellationToken ct)
    {
        var (ok, error, id) = await employees.SaveAsync(
            model, EmployeeEditScope.Hr, User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        if (!ok)
        {
            if (model.Id is int pid && await employees.GetByIdAsync(pid, ct) is { } stored)
                EmployeeProfilePolicy.MergeReadOnly(model, stored, EmployeeEditScope.Hr);
            else if (!string.IsNullOrEmpty(model.UserId) && await employees.GetByUserIdAsync(model.UserId, ct) is { } byUser)
                EmployeeProfilePolicy.MergeReadOnly(model, byUser, EmployeeEditScope.Hr);
            ModelState.AddModelError(string.Empty, error ?? "Không lưu được.");
            return PartialView("_EmployeeForm", model);
        }
        return Json(new
        {
            ok = true,
            message = model.Id is null && string.IsNullOrEmpty(model.UserId)
                ? "Đã thêm nhân viên."
                : "Đã lưu hồ sơ nhân viên.",
            redirect = Url.Action(nameof(Index), new { area = "Admin" })
        });
    }

    [HttpGet]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> GrantAccount(int id, CancellationToken ct)
    {
        var profile = await employees.GetByIdAsync(id, ct);
        if (profile is null) return NotFound();
        if (profile.HasAccount) return BadRequest("Nhân viên đã có tài khoản.");
        return PartialView("_GrantAccountForm", new GrantEmployeeAccountRequest
        {
            ProfileId = id,
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> GrantAccount(GrantEmployeeAccountRequest model, CancellationToken ct)
    {
        var (ok, error) = await employees.GrantAccountAsync(
            model, User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không cấp được tài khoản.");
            return PartialView("_GrantAccountForm", model);
        }
        return Json(new
        {
            ok = true,
            message = "Đã cấp tài khoản cho nhân viên.",
            redirect = Url.Action(nameof(Index), new { area = "Admin" })
        });
    }
}
