using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Abstractions;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
[HasPermission(Permissions.Help.View)]
public class HelpController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Hướng dẫn";
        return View();
    }
}
