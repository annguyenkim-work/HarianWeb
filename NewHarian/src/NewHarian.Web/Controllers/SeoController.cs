using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Controllers;

/// <summary>SEO baseline: robots.txt + sitemap.xml (NFR-07/08). Ngôn ngữ qua ?lang= nên hreflang dùng query variants.</summary>
public class SeoController(AppDbContext db, IMemoryCache cache) : Controller
{
    private static readonly string[] Langs = ["vi", "en", "ja"];

    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 3600)]
    public IActionResult Robots()
    {
        var baseUrl = BaseUrl();
        var sb = new StringBuilder()
            .AppendLine("User-agent: *")
            .AppendLine("Disallow: /admin/")
            .AppendLine("Disallow: /cart")
            .AppendLine("Disallow: /checkout")
            .AppendLine("Disallow: /orders/")
            .AppendLine("Disallow: /api/")
            .AppendLine()
            .AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var baseUrl = BaseUrl();
        var xml = await cache.GetOrCreateAsync($"seo.sitemap.{baseUrl}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await BuildSitemapAsync(baseUrl, ct);
        });
        return Content(xml!, "application/xml", Encoding.UTF8);
    }

    private async Task<string> BuildSitemapAsync(string baseUrl, CancellationToken ct)
    {
        var urls = new List<(string Path, DateTime? LastMod)>
        {
            ("/", null),
            ("/products", null),
            ("/about", null),
            ("/about/concept", null),
            ("/about/quality", null),
            ("/company", null),
            ("/contact", null),
            ("/careers", null),
            ("/dealers/register", null),
            ("/news", null),
            ("/legal/privacy", null),
            ("/legal/terms", null)
        };

        urls.Add(("/services", null));
        urls.Add(("/products", null));

        var categories = await db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Slug, c.UpdatedAt, c.CreatedAt })
            .ToListAsync(ct);
        urls.AddRange(categories.Select(c => ($"/products/{c.Slug}", (DateTime?)(c.UpdatedAt ?? c.CreatedAt))));
        urls.AddRange(categories.Select(c => ($"/services/{c.Slug}", (DateTime?)(c.UpdatedAt ?? c.CreatedAt))));

        var products = await db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published && p.Category.IsActive)
            .Select(p => new { p.Slug, CategorySlug = p.Category.Slug, p.UpdatedAt, p.CreatedAt })
            .ToListAsync(ct);
        urls.AddRange(products.Select(p =>
            ($"/products/{p.CategorySlug}/{p.Slug}", (DateTime?)(p.UpdatedAt ?? p.CreatedAt))));

        var servicesList = await db.Services.AsNoTracking()
            .Where(s => s.Status == ProductStatus.Published && s.Category.IsActive)
            .Select(s => new { s.Slug, CategorySlug = s.Category.Slug, s.UpdatedAt, s.CreatedAt })
            .ToListAsync(ct);
        urls.AddRange(servicesList.Select(s =>
            ($"/services/{s.CategorySlug}/{s.Slug}", (DateTime?)(s.UpdatedAt ?? s.CreatedAt))));

        var posts = await db.SitePosts.AsNoTracking()
            .Where(p => p.IsPublished)
            .Select(p => new { p.Kind, p.Slug, p.UpdatedAt, p.PublishedAt, p.CreatedAt })
            .ToListAsync(ct);
        urls.AddRange(posts.Select(p =>
        {
            var prefix = p.Kind == PostKind.Job ? "/careers" : "/news";
            return ($"{prefix}/{p.Slug}", (DateTime?)(p.UpdatedAt ?? p.PublishedAt ?? p.CreatedAt));
        }));

        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">""");
        foreach (var (path, lastMod) in urls)
        {
            var loc = baseUrl + path;
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{Escape(loc)}</loc>");
            foreach (var lang in Langs)
                sb.AppendLine($"""    <xhtml:link rel="alternate" hreflang="{lang}" href="{Escape($"{loc}?lang={lang}")}" />""");
            sb.AppendLine($"""    <xhtml:link rel="alternate" hreflang="x-default" href="{Escape(loc)}" />""");
            if (lastMod is not null)
                sb.AppendLine($"    <lastmod>{lastMod.Value:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("  </url>");
        }
        sb.AppendLine("</urlset>");
        return sb.ToString();
    }

    private string BaseUrl() => $"{Request.Scheme}://{Request.Host}";

    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
