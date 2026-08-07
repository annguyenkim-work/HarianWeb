using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Payments;

namespace NewHarian.Web.Controllers;

public class PaymentsController(IVnPayService vnpay) : Controller
{
    [HttpGet("/checkout/payment/{orderNumber}")]
    public IActionResult Pay(string orderNumber)
    {
        if (!vnpay.IsEnabled)
            return NotFound("VNPay chưa bật. Cấu hình Payment:VnPay trong appsettings.");
        // Full order lookup + redirect wired when OnlineGateway checkout is enabled.
        return Content("VNPay scaffold sẵn sàng - bật Payment:VnPay:Enabled và tích hợp OnlineGateway ở checkout.");
    }

    [HttpGet("/checkout/payment/callback")]
    public IActionResult Callback()
    {
        if (!vnpay.IsEnabled) return NotFound();
        var dict = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        var ok = vnpay.ValidateReturn(dict);
        return Content(ok ? "VNPay OK (scaffold)" : "VNPay invalid (scaffold)");
    }
}
