using Microsoft.AspNetCore.Mvc;

namespace NewHarian.Web.ViewComponents;

public class CookieConsentViewComponent : ViewComponent
{
    public const string CookieName = "cookie_consent";

    public IViewComponentResult Invoke()
    {
        if (Request.Cookies.ContainsKey(CookieName))
            return Content(string.Empty);
        return View();
    }
}
