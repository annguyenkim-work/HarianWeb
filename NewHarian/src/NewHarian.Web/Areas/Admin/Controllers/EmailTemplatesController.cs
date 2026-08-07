using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Email;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EmailTemplatesController(IEmailTemplateService templates) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Mẫu email";
        return View(await templates.ListAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await templates.GetForEditAsync(id, ct);
        if (model is null) return NotFound();
        ViewData["Title"] = "Sửa mẫu email";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EmailTemplateSaveRequest model, CancellationToken ct)
    {
        var (ok, error) = await templates.SaveAsync(model, ct);
        if (!ok)
        {
            var existing = await templates.GetForEditAsync(model.Id, ct);
            if (existing is null) return NotFound();
            ModelState.AddModelError(string.Empty, error ?? "Không lưu được.");
            ViewData["Title"] = "Sửa mẫu email";
            return View(new EmailTemplateEditDto(
                existing.Id, existing.Code, existing.Name, existing.PlaceholdersHelp,
                model.SubjectTemplate, model.BodyHtml));
        }

        TempData["Success"] = "Đã lưu mẫu email.";
        return RedirectToAction(nameof(Index), new { area = "Admin" });
    }
}
