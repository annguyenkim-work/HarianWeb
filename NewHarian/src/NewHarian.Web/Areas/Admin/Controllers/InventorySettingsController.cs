using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Inventory;
using NewHarian.Application.Settings;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[HasPermission(Permissions.Inventory.ManageSettings)]
public class InventorySettingsController(ISiteSettingsService settings, IInventoryService inventory) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Locations = await inventory.ListLocationsAsync(ct);
        return View(await settings.GetInventoryAsync(ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(InventorySettingsDto model, CancellationToken ct)
    {
        var result = await settings.SaveInventoryAsync(model, ct);
        if (!result.Ok)
        {
            foreach (var error in result.Errors ?? new Dictionary<string, string>())
                ModelState.AddModelError(error.Key, error.Value);
            ViewBag.Locations = await inventory.ListLocationsAsync(ct);
            return View(model);
        }

        this.FlashSuccess("Đã lưu cài đặt kho.");
        return RedirectToAction(nameof(Index), new { area = "Admin" });
    }
}
