using Microsoft.AspNetCore.Mvc;

namespace NewHarian.Web.Controllers;

/// <summary>Legacy /company URL; content lives on /about.</summary>
public class CompanyController : Controller
{
    [HttpGet("/company")]
    public IActionResult Index() => RedirectPermanent("/about");
}
