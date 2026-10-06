using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Dashboard;
using NewHarian.Infrastructure.Dashboard;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[HasPermission(Permissions.Dashboard.View)]
public class HomeController(IAdminDashboardService dashboard) : Controller
{
    public async Task<IActionResult> Index(DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        var showRevenue = User.HasPermission(Permissions.Dashboard.ViewRevenue);
        var range = AdminDashboardService.NormalizeRange(start, end);
        var model = await dashboard.GetAsync(
            range.Start, range.End,
            includeCharts: showRevenue,
            includeInventory: User.HasPermission(Permissions.Inventory.View),
            ct);
        ViewBag.ShowRevenue = showRevenue;
        return View(model);
    }
}
