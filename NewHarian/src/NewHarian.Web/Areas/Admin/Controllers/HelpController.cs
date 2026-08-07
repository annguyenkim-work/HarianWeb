using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public class HelpController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Hướng dẫn";
        ViewBag.IsAdmin = User.IsInRole(AppRoles.Admin);
        return View();
    }
}
