using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Cms;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.ViewComponents;

public class SiteHeaderViewComponent(AppDbContext db, ICmsPageService cms) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var keys = new[] { "company.brand", "company.logo", "company.name" };
        var map = await db.SiteSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var brand = map.GetValueOrDefault("company.brand");
        if (string.IsNullOrWhiteSpace(brand)) brand = "Harian";
        var logo = map.GetValueOrDefault("company.logo") ?? "";
        var company = map.GetValueOrDefault("company.name") ?? brand;

        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (lang is not ("en" or "ja")) lang = "vi";
        var nav = await cms.GetHeaderNavAsync(lang);
        if (nav.Count == 0)
        {
            // Fallback before seed / if header-main missing
            nav =
            [
                new PublicMenuItemDto("Home", "/", 1, "home"),
                new PublicMenuItemDto("Products", "/products", 2, "products"),
                new PublicMenuItemDto("Services", "/services", 3, "services"),
                new PublicMenuItemDto("About", "/about", 4, "about",
                [
                    new PublicMenuItemDto("About", "/about", 1, "about-page"),
                    new PublicMenuItemDto("Concept", "/about/concept", 2, "concept"),
                    new PublicMenuItemDto("Quality", "/about/quality", 3, "quality"),
                    new PublicMenuItemDto("Company", "/company", 4, "company")
                ]),
                new PublicMenuItemDto("News", "/news", 5, "news"),
                new PublicMenuItemDto("Careers", "/careers", 6, "careers"),
                new PublicMenuItemDto("Dealers", "/dealers/home", 7, "dealers"),
                new PublicMenuItemDto("Contact", "/contact", 8, "contact"),
                new PublicMenuItemDto("Track", "/Orders/Track", 9, "order-track")
            ];
        }

        return View(new SiteHeaderVm(brand!, logo, company!, nav));
    }

    public record SiteHeaderVm(
        string BrandName,
        string LogoUrl,
        string CompanyName,
        IReadOnlyList<PublicMenuItemDto> NavItems);
}
