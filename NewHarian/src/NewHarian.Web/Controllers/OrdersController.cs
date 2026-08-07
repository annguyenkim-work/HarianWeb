using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Orders;
using NewHarian.Application.Validation;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Web.Controllers;

public class OrdersController(IOrderService orders, AppDbContext db) : Controller
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
        await LoadBankAsync(ct);
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

    private async Task LoadBankAsync(CancellationToken ct)
    {
        ViewBag.BankName = await GetSettingAsync("company.bank.name", ct);
        ViewBag.BankAccount = await GetSettingAsync("company.bank.account", ct);
        ViewBag.BankBranch = await GetSettingAsync("company.bank.branch", ct);
        ViewBag.BankQr = await GetSettingAsync("company.bank.qr", ct);
    }

    private async Task<string?> GetSettingAsync(string key, CancellationToken ct)
        => await db.SiteSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    public class TrackForm
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
    }
}
