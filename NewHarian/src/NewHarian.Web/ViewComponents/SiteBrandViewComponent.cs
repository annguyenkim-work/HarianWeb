using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Cms;

namespace NewHarian.Web.ViewComponents;

public class SiteBrandViewComponent(ISiteChromeCache chrome) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var map = await chrome.GetSettingsAsync();

        var brand = map.GetValueOrDefault("company.brand");
        if (string.IsNullOrWhiteSpace(brand)) brand = "Harian";
        var logo = map.GetValueOrDefault("company.logo") ?? "";
        var company = map.GetValueOrDefault("company.name") ?? brand;

        return View(new SiteBrandVm(brand!, logo, company));
    }

    public record SiteBrandVm(string BrandName, string LogoUrl, string CompanyName);
}
