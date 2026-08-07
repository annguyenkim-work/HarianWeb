using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.ViewComponents;

public class SiteBrandViewComponent(AppDbContext db) : ViewComponent
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

        return View(new SiteBrandVm(brand!, logo, company));
    }

    public record SiteBrandVm(string BrandName, string LogoUrl, string CompanyName);
}
