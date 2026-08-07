using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Engagement;
using NewHarian.Application.Posts;
using NewHarian.Application.Shipping;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Controllers;

public class CareersController(
    IJobApplicationService apps,
    ISitePostService posts,
    IMediaStorage media,
    IShippingService shipping) : Controller
{
    private const string SessionKey = "careers.draft";
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    private async Task LoadProvincesAsync(CancellationToken ct)
        => ViewBag.Provinces = await shipping.GetActiveProvincesAsync(Lang, ct);

    [HttpGet("/careers")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Jobs = await posts.ListPublishedAsync(PostKind.Job, Lang, ct);
        return View();
    }

    [HttpGet("/careers/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        var post = await posts.GetPublishedBySlugAsync(PostKind.Job, slug, Lang, ct);
        if (post is null) return NotFound();
        ViewData["Title"] = post.Title;
        return View(post);
    }

    [HttpGet("/careers/{slug}/apply")]
    public async Task<IActionResult> Apply(string slug, CancellationToken ct)
    {
        var post = await posts.GetPublishedBySlugAsync(PostKind.Job, slug, Lang, ct);
        if (post is null) return NotFound();

        var draft = HttpContext.Session.GetString(SessionKey);
        CareerFormModel model;
        if (!string.IsNullOrEmpty(draft))
        {
            model = System.Text.Json.JsonSerializer.Deserialize<CareerFormModel>(draft) ?? new CareerFormModel();
            if (model.SitePostId != post.Id)
                model = new CareerFormModel();
        }
        else
        {
            model = new CareerFormModel();
        }

        model.SitePostId = post.Id;
        model.JobSlug = post.Slug;
        model.JobTitle = post.Title;
        ViewData["Title"] = "Ứng tuyển";
        await LoadProvincesAsync(ct);
        return View(model);
    }

    private const long CvMaxBytes = MediaUploadLimits.MaxFileBytes;

    [HttpPost("/careers/{slug}/apply")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaUploadLimits.HttpRequestBytes)]
    public async Task<IActionResult> Apply(string slug, CareerFormModel model, IFormFile? cvFile, CancellationToken ct)
    {
        var post = await posts.GetPublishedBySlugAsync(PostKind.Job, slug, Lang, ct);
        if (post is null) return NotFound();

        model.SitePostId = post.Id;
        model.JobSlug = post.Slug;
        model.JobTitle = post.Title;

        if (!string.IsNullOrWhiteSpace(model.Website))
        {
            HttpContext.Session.Remove(SessionKey);
            return RedirectToAction(nameof(ThankYou));
        }

        if (cvFile is { Length: > 0 })
        {
            if (cvFile.Length > CvMaxBytes)
            {
                ModelState.AddModelError(string.Empty, $"File CV tối đa {MediaUploadLimits.MaxFileLabel}.");
                await LoadProvincesAsync(ct);
                return View(model);
            }
            try
            {
                await using var stream = cvFile.OpenReadStream();
                var up = await media.SaveDocumentAsync(stream, cvFile.FileName, cvFile.ContentType, null, ct);
                model.AttachmentMediaFileId = up.Id;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await LoadProvincesAsync(ct);
                return View(model);
            }
        }

        if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng kiểm tra họ tên và email.");
            await LoadProvincesAsync(ct);
            return View(model);
        }

        if (model.ApplicationType == ApplicationType.Application && model.AttachmentMediaFileId is null or <= 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng đính kèm CV khi ứng tuyển.");
            await LoadProvincesAsync(ct);
            return View(model);
        }

        HttpContext.Session.SetString(SessionKey, System.Text.Json.JsonSerializer.Serialize(model));
        await HttpContext.Session.CommitAsync(ct);
        return RedirectToAction(nameof(Confirm));
    }

    [HttpGet("/careers/confirm")]
    public IActionResult Confirm()
    {
        var draft = HttpContext.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(draft)) return RedirectToAction(nameof(Index));
        var model = System.Text.Json.JsonSerializer.Deserialize<CareerFormModel>(draft);
        if (model is null || model.SitePostId is null) return RedirectToAction(nameof(Index));
        return View(model);
    }

    [HttpPost("/careers/submit")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("careers-form")]
    public async Task<IActionResult> Submit(CareerFormModel? formModel, CancellationToken ct)
    {
        // Prefer session draft; fall back to hidden fields posted from Confirm (session can be missing on some hosts).
        CareerFormModel? model = null;
        var draft = HttpContext.Session.GetString(SessionKey);
        if (!string.IsNullOrEmpty(draft))
            model = System.Text.Json.JsonSerializer.Deserialize<CareerFormModel>(draft);

        if (model is null || model.SitePostId is null or <= 0)
            model = formModel;

        if (model is null || model.SitePostId is null or <= 0)
        {
            TempData["Error"] = "Phiên xác nhận đã hết hạn. Vui lòng gửi lại hồ sơ.";
            return RedirectToAction(nameof(Index));
        }

        var (ok, error, _) = await apps.SubmitAsync(model, Lang, ct);
        if (!ok)
        {
            TempData["Error"] = error;
            if (!string.IsNullOrWhiteSpace(model.JobSlug))
                return RedirectToAction(nameof(Apply), new { slug = model.JobSlug });
            return RedirectToAction(nameof(Index));
        }

        HttpContext.Session.Remove(SessionKey);
        return RedirectToAction(nameof(ThankYou));
    }

    [HttpGet("/careers/thank-you")]
    public IActionResult ThankYou() => View();
}
