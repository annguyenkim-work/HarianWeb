using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public class NotificationsController(IAdminNotificationService notifications) : Controller
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index(int take = 20, CancellationToken ct = default)
    {
        var uid = UserId;
        if (uid is null) return Unauthorized();
        var items = await notifications.ListAsync(uid, take, ct);
        return Json(items);
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
    {
        var uid = UserId;
        if (uid is null) return Unauthorized();
        var count = await notifications.UnreadCountAsync(uid, ct);
        return Json(new { count });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct = default)
    {
        var uid = UserId;
        if (uid is null) return Unauthorized();
        await notifications.MarkReadAsync(uid, id, ct);
        return Json(new { ok = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var uid = UserId;
        if (uid is null) return Unauthorized();
        await notifications.MarkAllReadAsync(uid, ct);
        return Json(new { ok = true });
    }
}
