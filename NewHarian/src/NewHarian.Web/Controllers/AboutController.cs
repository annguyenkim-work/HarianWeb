using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Cms;

namespace NewHarian.Web.Controllers;

public class AboutController(ICmsPageService cms) : Controller
{
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("/about")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var page = await cms.GetPublishedBySlugAsync("about", Lang, ct);
        if (page is null) return NotFound();
        return View("Page", page);
    }

    [HttpGet("/about/concept")]
    public IActionResult Concept() => RedirectPermanent("/about");

    [HttpGet("/about/quality")]
    public IActionResult Quality() => RedirectPermanent("/about");
}
