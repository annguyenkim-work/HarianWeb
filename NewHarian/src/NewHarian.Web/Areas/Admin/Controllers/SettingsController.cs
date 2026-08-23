using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Inventory;
using NewHarian.Application.Settings;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
public class SettingsController(ISiteSettingsService settings, IInventoryService inventory) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await settings.GetBankAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BankSettingsDto model, CancellationToken ct)
    {
        await settings.SaveBankAsync(model, ct);
        TempData["Success"] = "Đã lưu thông tin ngân hàng / VietQR.";
        return RedirectToAction(nameof(Index), new { area = "Admin" });
    }

    public async Task<IActionResult> Brand(CancellationToken ct)
        => View(await settings.GetBrandAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Brand(
        BrandSettingsDto model,
        IFormFile? logoFile,
        CancellationToken ct)
    {
        if (logoFile is { Length: > 0 })
        {
            await using var stream = logoFile.OpenReadStream();
            await settings.SaveBrandAsync(model, new SettingsFileUpload(
                stream,
                logoFile.FileName,
                logoFile.ContentType,
                User.Identity?.Name), ct);
        }
        else
        {
            await settings.SaveBrandAsync(model, null, ct);
        }

        TempData["Success"] = "Đã lưu thương hiệu, footer và giao diện header.";
        return RedirectToAction(nameof(Brand), new { area = "Admin" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearLogo(CancellationToken ct)
    {
        await settings.ClearLogoAsync(ct);
        TempData["Success"] = "Đã gỡ logo. Header sẽ hiện tên thương hiệu dạng chữ.";
        return RedirectToAction(nameof(Brand), new { area = "Admin" });
    }

    public async Task<IActionResult> Email(CancellationToken ct)
        => View(await settings.GetEmailAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Email(EmailSettingsDto model, CancellationToken ct)
    {
        var result = await settings.SaveEmailAsync(model, ct);
        if (!result.Ok)
        {
            foreach (var error in result.Errors ?? new Dictionary<string, string>())
                ModelState.AddModelError(error.Key, error.Value);
            return View(model);
        }

        TempData["Success"] = "Đã lưu email nhận thông báo.";
        return RedirectToAction(nameof(Email), new { area = "Admin" });
    }

    public async Task<IActionResult> Inventory(CancellationToken ct)
    {
        ViewBag.Locations = await inventory.ListLocationsAsync(ct);
        return View(await settings.GetInventoryAsync(ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inventory(InventorySettingsDto model, CancellationToken ct)
    {
        var result = await settings.SaveInventoryAsync(model, ct);
        if (!result.Ok)
        {
            foreach (var error in result.Errors ?? new Dictionary<string, string>())
                ModelState.AddModelError(error.Key, error.Value);
            ViewBag.Locations = await inventory.ListLocationsAsync(ct);
            return View(model);
        }

        TempData["Success"] = "Đã lưu cài đặt kho.";
        return RedirectToAction(nameof(Inventory), new { area = "Admin" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestEmail(string? testTo, CancellationToken ct)
    {
        var result = await settings.TestEmailAsync(testTo, ct);
        TempData[result.Ok ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Email), new { area = "Admin" });
    }
}
