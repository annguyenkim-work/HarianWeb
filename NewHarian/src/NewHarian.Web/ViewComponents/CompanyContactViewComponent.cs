using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Cms;

namespace NewHarian.Web.ViewComponents;

public class CompanyContactViewComponent(ISiteChromeCache chrome) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var map = await chrome.GetSettingsAsync();
        var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (lang is not ("en" or "ja")) lang = "vi";

        var tagline = map.GetValueOrDefault($"company.tagline.{lang}");
        if (string.IsNullOrWhiteSpace(tagline))
            tagline = map.GetValueOrDefault("company.tagline.vi") ?? "";

        var brand = map.GetValueOrDefault("company.brand");
        if (string.IsNullOrWhiteSpace(brand)) brand = "Harian";
        var company = map.GetValueOrDefault("company.name");
        if (string.IsNullOrWhiteSpace(company)) company = brand;

        return View(new CompanyContactVm(
            company!,
            tagline ?? "",
            map.GetValueOrDefault("company.address") ?? "",
            map.GetValueOrDefault("company.email") ?? "",
            map.GetValueOrDefault("company.phone") ?? "",
            map.GetValueOrDefault("company.phone2") ?? ""));
    }

    public record CompanyContactVm(
        string CompanyName,
        string Tagline,
        string Address,
        string Email,
        string Phone,
        string Phone2);
}
