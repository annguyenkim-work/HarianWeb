using Microsoft.AspNetCore.Mvc;

namespace NewHarian.Web.Controllers;

public class HelpController : Controller
{
    [HttpGet("/help")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Hướng dẫn sử dụng";
        ViewData["MetaDescription"] = "Hướng dẫn mua hàng, thanh toán và tra cứu đơn trên website Harian Corporation.";
        return View();
    }
}
