using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NewHarian.Application.Cms;
using NewHarian.Application.Engagement;

namespace NewHarian.Web.Controllers;

public class ContactController(IInquiryService inquiries, ICmsPageService cms) : Controller
{
    private const string SessionKey = "contact.draft";
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("/contact")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        await LoadChromeAsync(ct);
        var draft = HttpContext.Session.GetString(SessionKey);
        var model = string.IsNullOrEmpty(draft)
            ? new ContactFormModel()
            : System.Text.Json.JsonSerializer.Deserialize<ContactFormModel>(draft) ?? new ContactFormModel();
        return View(model);
    }

    [HttpPost("/contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ContactFormModel model, CancellationToken ct)
    {
        await LoadChromeAsync(ct);
        if (!string.IsNullOrWhiteSpace(model.Website))
        {
            HttpContext.Session.Remove(SessionKey);
            return RedirectToAction(nameof(ThankYou));
        }

        if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Message) || model.Message.Trim().Length < 10)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng kiểm tra họ tên, email và nội dung (tối thiểu 10 ký tự).");
            return View(model);
        }

        HttpContext.Session.SetString(SessionKey, System.Text.Json.JsonSerializer.Serialize(model));
        await HttpContext.Session.CommitAsync(ct);
        return RedirectToAction(nameof(Confirm));
    }

    [HttpGet("/contact/confirm")]
    public async Task<IActionResult> Confirm(CancellationToken ct)
    {
        await LoadChromeAsync(ct);
        var draft = HttpContext.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(draft)) return RedirectToAction(nameof(Index));
        var model = System.Text.Json.JsonSerializer.Deserialize<ContactFormModel>(draft);
        if (model is null) return RedirectToAction(nameof(Index));
        return View(model);
    }

    [HttpPost("/contact/submit")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("contact-form")]
    public async Task<IActionResult> Submit(ContactFormModel? formModel, CancellationToken ct)
    {
        ContactFormModel? model = null;
        var draft = HttpContext.Session.GetString(SessionKey);
        if (!string.IsNullOrEmpty(draft))
            model = System.Text.Json.JsonSerializer.Deserialize<ContactFormModel>(draft);

        if (model is null || string.IsNullOrWhiteSpace(model.Email))
            model = formModel;

        if (model is null || string.IsNullOrWhiteSpace(model.Email))
        {
            TempData["Error"] = "Phiên xác nhận đã hết hạn. Vui lòng gửi lại.";
            return RedirectToAction(nameof(Index));
        }

        var (ok, error, _) = await inquiries.SubmitAsync(model, Lang, ct);
        if (!ok)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        HttpContext.Session.Remove(SessionKey);
        return RedirectToAction(nameof(ThankYou));
    }

    [HttpGet("/contact/thank-you")]
    public async Task<IActionResult> ThankYou(CancellationToken ct)
    {
        await LoadChromeAsync(ct);
        return View();
    }

    private async Task LoadChromeAsync(CancellationToken ct)
    {
        ViewBag.CmsPage = await cms.GetPublishedBySlugAsync("contact", Lang, ct);
    }
}
