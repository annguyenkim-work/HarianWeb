using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Cms;
using NewHarian.Application.Dealers;

namespace NewHarian.Web.Controllers;

public class DealersController(IDealerRegistrationService dealers, IMediaStorage media, ICmsPageService cms) : Controller
{
    private const string SessionKey = "dealers.draft";
    private const string MediaFolder = "dealers";
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("/dealers")]
    [HttpGet("/dealers/home")]
    public async Task<IActionResult> Home(CancellationToken ct)
    {
        var page = await cms.GetPublishedBySlugAsync("dealers/home", Lang, ct);
        if (page is null) return NotFound();
        return View("Home", page);
    }

    [HttpGet("/dealers/register")]
    public IActionResult Index() => View(ReadDraft() ?? new DealerFormModel());

    [HttpPost("/dealers/register")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("dealers-upload")]
    [RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
    public async Task<IActionResult> Index(DealerFormModel model, IFormFile? businessLicenseFile, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(model.Website))
        {
            HttpContext.Session.Remove(SessionKey);
            return RedirectToAction(nameof(ThankYou));
        }

        var previous = ReadDraft();
        model.BusinessLicenseMediaFileId = previous?.BusinessLicenseMediaFileId;
        model.BusinessLicenseFileName = previous?.BusinessLicenseFileName;

        var error = await dealers.ValidateAsync(model, ct);
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        if (businessLicenseFile is { Length: > 0 })
        {
            try
            {
                await using var stream = businessLicenseFile.OpenReadStream();
                var up = await media.SaveDocumentAsync(
                    stream, businessLicenseFile.FileName, businessLicenseFile.ContentType, null, ct, MediaFolder);
                model.BusinessLicenseMediaFileId = up.Id;
                model.BusinessLicenseFileName = up.FileName;
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(model));
        await HttpContext.Session.CommitAsync(ct);
        return RedirectToAction(nameof(Confirm));
    }

    [HttpGet("/dealers/confirm")]
    public IActionResult Confirm()
    {
        var model = ReadDraft();
        return model is null ? RedirectToAction(nameof(Index)) : View(model);
    }

    [HttpPost("/dealers/submit")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("dealers-form")]
    public async Task<IActionResult> Submit(DealerFormModel? formModel, CancellationToken ct)
    {
        var model = ReadDraft();
        if (model is null || string.IsNullOrWhiteSpace(model.Email))
            model = formModel;

        if (model is null || string.IsNullOrWhiteSpace(model.Email))
        {
            TempData["Error"] = "Phiên xác nhận đã hết hạn. Vui lòng gửi lại.";
            return RedirectToAction(nameof(Index));
        }

        var (ok, error, _) = await dealers.SubmitAsync(model, Lang, ct);
        if (!ok)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        HttpContext.Session.Remove(SessionKey);
        return RedirectToAction(nameof(ThankYou));
    }

    [HttpGet("/dealers/thank-you")]
    public IActionResult ThankYou() => View();

    private DealerFormModel? ReadDraft()
    {
        var draft = HttpContext.Session.GetString(SessionKey);
        return string.IsNullOrEmpty(draft) ? null : JsonSerializer.Deserialize<DealerFormModel>(draft);
    }
}
