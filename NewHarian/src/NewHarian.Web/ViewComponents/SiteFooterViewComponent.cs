using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.ViewComponents;

public class SiteFooterViewComponent(AppDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var keys = new[]
        {
            "company.name", "company.brand", "company.logo",
            "company.phone", "company.phone2", "company.email", "company.address",
            "company.tagline.vi", "company.tagline.en", "company.tagline.ja",
            "company.facebook", "company.instagram"
        };
        var map = await db.SiteSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (lang is not ("en" or "ja")) lang = "vi";

        string Tagline()
        {
            var v = map.GetValueOrDefault($"company.tagline.{lang}");
            if (!string.IsNullOrWhiteSpace(v)) return v!;
            return map.GetValueOrDefault("company.tagline.vi") ?? "";
        }

        var brand = map.GetValueOrDefault("company.brand");
        if (string.IsNullOrWhiteSpace(brand)) brand = "Harian";
        var company = map.GetValueOrDefault("company.name");
        if (string.IsNullOrWhiteSpace(company)) company = brand;

        var contactTitle = lang switch
        {
            "en" => "Contact",
            "ja" => "お問い合わせ",
            _ => "Liên hệ"
        };

        return View(new SiteFooterVm(
            company!,
            brand!,
            map.GetValueOrDefault("company.logo") ?? "",
            Tagline(),
            map.GetValueOrDefault("company.address") ?? "",
            map.GetValueOrDefault("company.email") ?? "",
            map.GetValueOrDefault("company.phone") ?? "",
            map.GetValueOrDefault("company.phone2") ?? "",
            map.GetValueOrDefault("company.facebook") ?? "",
            map.GetValueOrDefault("company.instagram") ?? "",
            contactTitle
        ));
    }

    public record SiteFooterVm(
        string CompanyName,
        string BrandName,
        string LogoUrl,
        string Tagline,
        string Address,
        string Email,
        string Phone,
        string Phone2,
        string FacebookUrl,
        string InstagramUrl,
        string ContactHeading);
}
