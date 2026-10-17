using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Analytics;
using NewHarian.Infrastructure.Analytics;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[HasPermission(Permissions.Analytics.View)]
public class AnalyticsController(IAdminAnalyticsService analytics) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        var range = AnalyticsTime.NormalizeRange(start, end);
        var model = await analytics.GetAsync(range.Start, range.End, ct);
        return View(model);
    }
}
