using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Posts;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Controllers;

public class NewsController(ISitePostService posts) : Controller
{
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("/news")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Tin tức";
        return View(await posts.ListPublishedAsync(PostKind.News, Lang, ct));
    }

    [HttpGet("/news/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        var post = await posts.GetPublishedBySlugAsync(PostKind.News, slug, Lang, ct);
        if (post is null) return NotFound();
        ViewData["Title"] = post.Title;
        return View(post);
    }
}
