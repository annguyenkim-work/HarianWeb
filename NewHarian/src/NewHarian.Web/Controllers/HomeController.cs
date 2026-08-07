using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Catalog;
using NewHarian.Application.Cms;
using NewHarian.Web.Models;

namespace NewHarian.Web.Controllers;

public class HomeController(ICmsPageService cms, ICatalogService catalog) : Controller
{
    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var page = await cms.GetPublishedBySlugAsync("home", Lang, ct);
        var categories = await catalog.GetHomeCategoriesAsync(Lang, ct);
        var featured = await catalog.GetFeaturedProductsAsync(Lang, ct: ct);
        ViewBag.Categories = categories;
        ViewBag.Featured = featured;
        ViewBag.Slides = await cms.GetActiveHomeSlidesAsync(Lang, ct);
        return View(page);
    }

    [HttpPost]
    public IActionResult SetLanguage(string culture, string returnUrl = "/")
    {
        Response.Cookies.Append(
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
                new Microsoft.AspNetCore.Localization.RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, Path = "/" });

        if (!Url.IsLocalUrl(returnUrl))
            returnUrl = "/";

        return LocalRedirect(returnUrl);
    }

    /// <summary>Unhandled exceptions (UseExceptionHandler) — production.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("StatusCode", new ErrorViewModel
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    /// <summary>404 / other status codes via UseStatusCodePagesWithReExecute.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCodePage(int code = StatusCodes.Status404NotFound)
    {
        if (code < 400 || code > 599)
            code = StatusCodes.Status404NotFound;

        Response.StatusCode = code;
        return View("StatusCode", new ErrorViewModel
        {
            StatusCode = code,
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
