using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Orders;
using NewHarian.Application.Payments;
using NewHarian.Application.Validation;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Controllers;

public class OrdersController(IOrderService orders, IBankTransferDisplayService bankTransfer) : Controller
{
    [HttpGet("/orders/track")]
    public IActionResult Track() => View(new TrackForm());

    [HttpPost("/orders/track")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Track(TrackForm form, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(form.OrderNumber) || string.IsNullOrWhiteSpace(form.CustomerEmail))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng nhập mã đơn và email.");
            return View(form);
        }
        if (!GuestValidation.IsOrderNumber(form.OrderNumber) || !GuestValidation.IsEmail(form.CustomerEmail))
        {
            ViewBag.NotFound = true;
            return View(form);
        }

        var order = await orders.TrackAsync(form.OrderNumber, form.CustomerEmail, ct);
        if (order is null)
        {
            ViewBag.NotFound = true;
            return View(form);
        }

        ViewBag.Order = order;
        if (order.Status == OrderStatus.PendingPayment && order.PaymentMethod == PaymentMethod.BankTransfer)
        {
            var bank = await bankTransfer.BuildAsync(order.Total, order.OrderNumber, ct);
            ViewBag.BankName = bank.BankName;
            ViewBag.BankAccount = bank.BankAccount;
            ViewBag.BankBranch = bank.BankBranch;
            ViewBag.AccountHolderName = bank.AccountHolderName;
            ViewBag.BankQr = bank.QrSrc;
            ViewBag.AmountText = bank.AmountText;
            ViewBag.TransferContent = bank.TransferContent;
        }
        return View(form);
    }

    [HttpPost("/orders/track/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string orderNumber, string customerEmail, CancellationToken ct)
    {
        if (!GuestValidation.IsOrderNumber(orderNumber) || !GuestValidation.IsEmail(customerEmail))
        {
            TempData["Error"] = "Thông tin đơn hàng không hợp lệ.";
            return RedirectToAction(nameof(Track));
        }
        var (ok, error) = await orders.CancelGuestAsync(orderNumber, customerEmail, ct);
        TempData[ok ? "Success" : "Error"] = ok ? "Đã hủy đơn hàng." : error;
        return RedirectToAction(nameof(Track));
    }

    public class TrackForm
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
    }
}
