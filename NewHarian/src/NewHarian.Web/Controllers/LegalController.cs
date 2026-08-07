using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Cms;

namespace NewHarian.Web.Controllers;

public class LegalController(ICmsPageService cms) : Controller
{
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("/legal/privacy")]
    public Task<IActionResult> Privacy(CancellationToken ct) => Page("legal/privacy", ct);

    [HttpGet("/legal/terms")]
    public Task<IActionResult> Terms(CancellationToken ct) => Page("legal/terms", ct);

    private async Task<IActionResult> Page(string slug, CancellationToken ct)
    {
        var page = await cms.GetPublishedBySlugAsync(slug, Lang, ct);
        if (page is null) return NotFound();
        ViewBag.BreadcrumbTwoLevel = true;
        return View("~/Views/Shared/CmsContentPage.cshtml", page);
    }
}
