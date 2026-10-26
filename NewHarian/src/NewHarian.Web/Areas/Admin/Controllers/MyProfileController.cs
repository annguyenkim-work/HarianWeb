using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Hr;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

/// <summary>"Hồ sơ của tôi": the signed-in user fills in the personal part of their own employee profile.</summary>
[Area("Admin")]
[HasPermission(Permissions.MyProfile.Edit)]
public class MyProfileController(IAdminEmployeeService employees) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await employees.GetByUserIdAsync(CurrentUserId, ct);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(EmployeeProfileForm model, CancellationToken ct)
    {
        model.UserId = CurrentUserId;
        // Self cannot create a floating profile; Id comes from stored profile or remains null for legacy first save.
        var storedBefore = await employees.GetByUserIdAsync(CurrentUserId, ct);
        if (storedBefore is null) return NotFound();
        model.Id = storedBefore.Id;

        var canChangeEmail = User.HasPermission(Permissions.Employees.ChangeEmail);
        var (ok, error, _) = await employees.SaveAsync(
            model, EmployeeEditScope.Self, CurrentUserId, canChangeEmail, ct);
        if (ok)
        {
            this.FlashSuccess("Đã lưu hồ sơ của bạn.");
            return RedirectToAction(nameof(Index));
        }

        var stored = await employees.GetByUserIdAsync(CurrentUserId, ct);
        if (stored is null) return NotFound();
        EmployeeProfilePolicy.MergeReadOnly(model, stored, EmployeeEditScope.Self, lockPersonalEmail: !canChangeEmail);
        ModelState.AddModelError(string.Empty, error ?? "Không lưu được.");
        return View(model);
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
