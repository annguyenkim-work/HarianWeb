using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Cms;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Controllers;

public class CompanyController(ICmsPageService cms, AppDbContext db) : Controller
{
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("/company")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var page = await cms.GetPublishedBySlugAsync("company", Lang, ct);
        if (page is null) return NotFound();

        ViewBag.SectionTitleKey = "Company.LegalInfoSectionTitle";
        ViewBag.MapsEmbedUrl = await GetSafeMapsEmbedAsync(ct);
        return View("~/Views/Shared/CmsContentPage.cshtml", page);
    }

    private async Task<string?> GetSafeMapsEmbedAsync(CancellationToken ct)
    {
        var url = await db.SiteSettings.AsNoTracking()
            .Where(s => s.Key == "maps.embed_url")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("https" or "http")) return null;
        var host = uri.Host.ToLowerInvariant();
        if (host is not ("www.google.com" or "google.com" or "maps.google.com" or "www.google.com"))
        {
            // allow maps.google.com and google.com/maps embed hosts
            if (!host.EndsWith(".google.com") && host != "google.com")
                return null;
        }
        return url;
    }
}
