using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Cms;

namespace NewHarian.Web.Controllers;

public class AboutController(ICmsPageService cms) : Controller
{
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("/about")]
    public Task<IActionResult> Index(CancellationToken ct) => Page("about", ct);

    [HttpGet("/about/concept")]
    public Task<IActionResult> Concept(CancellationToken ct) => Page("about/concept", ct);

    [HttpGet("/about/quality")]
    public Task<IActionResult> Quality(CancellationToken ct) => Page("about/quality", ct);

    private async Task<IActionResult> Page(string slug, CancellationToken ct)
    {
        var page = await cms.GetPublishedBySlugAsync(slug, Lang, ct);
        if (page is null) return NotFound();
        return View("Page", page);
    }
}
