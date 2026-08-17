using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NewHarian.Application.Cms;
using NewHarian.Application.Dealers;
using NewHarian.Application.Validation;

namespace NewHarian.Web.Controllers;

public class DealersController(IDealerService dealers, ICmsPageService cms) : Controller
{
    private const string SessionKey = "dealers.draft";
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
    public IActionResult Index()
    {
        var draft = HttpContext.Session.GetString(SessionKey);
        var model = string.IsNullOrEmpty(draft)
            ? new DealerFormModel()
            : JsonSerializer.Deserialize<DealerFormModel>(draft) ?? new DealerFormModel();
        return View(model);
    }

    [HttpPost("/dealers/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(DealerFormModel model, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(model.Website))
        {
            HttpContext.Session.Remove(SessionKey);
            return RedirectToAction(nameof(ThankYou));
        }

        if (!GuestValidation.HasLength(model.FullName, 2, GuestValidation.NameMax)
            || string.IsNullOrWhiteSpace(model.Phone) || !GuestValidation.IsPhone(model.Phone)
            || !GuestValidation.IsEmail(model.Email)
            || !GuestValidation.IsCitizenId(model.CitizenId)
            || !GuestValidation.HasLength(model.Address, 5, GuestValidation.AddressMax)
            || !GuestValidation.FitsMax(model.Message, GuestValidation.NotesMax))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng kiểm tra họ tên, SĐT, email, CCCD và địa chỉ.");
            return View(model);
        }

        HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(model));
        await HttpContext.Session.CommitAsync(ct);
        return RedirectToAction(nameof(Confirm));
    }

    [HttpGet("/dealers/confirm")]
    public IActionResult Confirm()
    {
        var draft = HttpContext.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(draft)) return RedirectToAction(nameof(Index));
        var model = JsonSerializer.Deserialize<DealerFormModel>(draft);
        if (model is null) return RedirectToAction(nameof(Index));
        return View(model);
    }

    [HttpPost("/dealers/submit")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("dealers-form")]
    public async Task<IActionResult> Submit(DealerFormModel? formModel, CancellationToken ct)
    {
        DealerFormModel? model = null;
        var draft = HttpContext.Session.GetString(SessionKey);
        if (!string.IsNullOrEmpty(draft))
            model = JsonSerializer.Deserialize<DealerFormModel>(draft);

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
}
