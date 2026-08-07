using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Dashboard;
using NewHarian.Infrastructure.Dashboard;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public class HomeController(IAdminDashboardService dashboard) : Controller
{
    public async Task<IActionResult> Index(DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var range = AdminDashboardService.NormalizeRange(start, end);
        var model = await dashboard.GetAsync(range.Start, range.End, includeCharts: isAdmin, ct);
        ViewBag.IsAdmin = isAdmin;
        return View(model);
    }
}
