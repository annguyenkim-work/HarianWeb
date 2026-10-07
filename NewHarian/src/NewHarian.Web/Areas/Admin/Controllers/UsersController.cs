using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[HasPermission(Permissions.Users.Manage)]
public class UsersController(IAdminUserService users) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var (items, pager) = AdminPaging.Apply(await users.ListAsync(ct), page);
        ViewBag.Pager = pager;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string? id, CancellationToken ct)
    {
        if (id is null)
            return PartialView("_UserForm", new AdminUserSaveRequest());

        var model = await users.GetForEditAsync(id, ct);
        if (model is null) return NotFound();
        return PartialView("_UserForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminUserSaveRequest model, CancellationToken ct)
    {
        var (ok, error, _) = await users.SaveAsync(model, User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Không lưu được.");
            model.Password = null;
            return PartialView("_UserForm", model);
        }
        return Json(new
        {
            ok = true,
            message = model.Id is null ? "Đã thêm user." : "Đã cập nhật user.",
            redirect = Url.Action(nameof(Index), new { area = "Admin" })
        });
    }
}
