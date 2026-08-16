using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NewHarian.Application.Address;
using NewHarian.Application.Cart;
using NewHarian.Application.Orders;
using NewHarian.Application.Payments;
using NewHarian.Application.Shipping;
using NewHarian.Application.Validation;
using NewHarian.Domain.Enums;

namespace NewHarian.Web.Controllers;

public class CheckoutController(
    ICartService cart,
    IShippingService shipping,
    IOrderService orders,
    IBankTransferDisplayService bankTransfer,
    IVietnamDivisionCatalog catalog) : Controller
{
    private const string DraftKey = "CheckoutDraft";
    private const string LastOrderKey = "LastPlacedOrder";

    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("/checkout")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var basket = cart.GetCart();
        if (basket.Items.Count == 0) return RedirectToAction("Index", "Cart");

        ViewBag.Cart = basket;
        ViewBag.FreeThreshold = await shipping.GetFreeThresholdAsync(ct);

        var draft = LoadDraft() ?? new CheckoutDraft
        {
            PaymentMethod = PaymentMethod.COD,
            LanguageCode = Lang
        };
        return View(draft);
    }

    [HttpPost("/checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutDraft model, CancellationToken ct)
    {
        var basket = cart.GetCart();
        if (basket.Items.Count == 0) return RedirectToAction("Index", "Cart");

        model.LanguageCode = Lang;
        var error = ValidateDraft(model);
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            ViewBag.Cart = basket;
            ViewBag.FreeThreshold = await shipping.GetFreeThresholdAsync(ct);
            return View(model);
        }

        model.CheckoutId = Guid.NewGuid().ToString("N");
        SaveDraft(model);
        return RedirectToAction(nameof(Confirm));
    }

    [HttpGet("/checkout/shipping-fee")]
    public async Task<IActionResult> ShippingFee(string provinceCode, CancellationToken ct)
    {
        var basket = cart.GetCart();
        var (fee, isFree) = await shipping.CalculateFeeAsync(basket.SubTotal, provinceCode ?? "", ct);
        return Json(new
        {
            fee,
            feeText = fee.ToString("N0") + "đ",
            isFree,
            subTotal = basket.SubTotal,
            subTotalText = basket.SubTotal.ToString("N0") + "đ",
            total = basket.SubTotal + fee,
            totalText = (basket.SubTotal + fee).ToString("N0") + "đ"
        });
    }

    [HttpGet("/checkout/confirm")]
    public async Task<IActionResult> Confirm(CancellationToken ct)
    {
        var draft = LoadDraft();
        var basket = cart.GetCart();
        if (draft is null || basket.Items.Count == 0) return RedirectToAction(nameof(Index));

        var (fee, isFree) = await shipping.CalculateFeeAsync(basket.SubTotal, draft.ShippingProvinceCode, ct);
        catalog.TryResolve(draft.ShippingProvinceCode, draft.ShippingCommuneCode, out var addr);
        ViewBag.Cart = basket;
        ViewBag.ShippingFee = fee;
        ViewBag.IsFreeShipping = isFree;
        ViewBag.ShipTo = AddressFormat.Join(draft.ShippingAddress, addr.CommuneName, addr.ProvinceName);
        ViewBag.Total = basket.SubTotal + fee;
        return View(draft);
    }

    [HttpPost("/checkout/submit")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout-submit")]
    public async Task<IActionResult> Submit(CancellationToken ct)
    {
        var draft = LoadDraft();
        if (draft is null) return RedirectToAction(nameof(Index));

        // Idempotent: same checkout id already placed
        var last = HttpContext.Session.GetString(LastOrderKey);
        if (!string.IsNullOrEmpty(last))
        {
            var parts = last.Split('|');
            if (parts.Length == 2 && parts[0] == draft.CheckoutId)
                return RedirectToAction(nameof(Success), new { orderNumber = parts[1] });
        }

        var (ok, error, orderNumber) = await orders.PlaceOrderAsync(draft, ct);
        if (!ok || orderNumber is null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Confirm));
        }

        HttpContext.Session.SetString(LastOrderKey, $"{draft.CheckoutId}|{orderNumber}");
        HttpContext.Session.Remove(DraftKey);
        return RedirectToAction(nameof(Success), new { orderNumber });
    }

    [HttpGet("/checkout/success/{orderNumber}")]
    public async Task<IActionResult> Success(string orderNumber, CancellationToken ct)
    {
        var order = await orders.GetByOrderNumberAsync(orderNumber, ct);
        if (order is null) return NotFound();

        if (order.PaymentMethod == PaymentMethod.BankTransfer)
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
        return View(order);
    }

    private string? ValidateDraft(CheckoutDraft m)
    {
        if (!GuestValidation.HasLength(m.CustomerName, 2, GuestValidation.NameMax))
            return "Vui lòng điền họ tên (2-200 ký tự)."; // CHK_REQUIRED
        if (!GuestValidation.IsEmail(m.CustomerEmail))
            return "Email không hợp lệ."; // CHK_EMAIL_INVALID
        if (string.IsNullOrWhiteSpace(m.CustomerPhone) || !GuestValidation.IsPhone(m.CustomerPhone))
            return "Số điện thoại không hợp lệ (8-20 ký tự, chỉ số/khoảng trắng/+/-)."; // CHK_PHONE_INVALID
        if (!GuestValidation.IsCitizenId(m.CitizenId))
            return "CCCD phải gồm 9 hoặc 12 chữ số."; // CHK_CITIZEN_ID_INVALID
        var addrErr = AddressFormat.Require(catalog, m.ShippingProvinceCode, m.ShippingCommuneCode, m.ShippingAddress);
        if (addrErr is not null)
            return addrErr; // CHK_REQUIRED / CHK_PROVINCE_REQUIRED
        if (!GuestValidation.FitsMax(m.Notes, GuestValidation.NotesMax))
            return "Ghi chú tối đa 2000 ký tự.";
        if (m.PaymentMethod is not (PaymentMethod.COD or PaymentMethod.BankTransfer))
            return "Vui lòng chọn phương thức thanh toán."; // CHK_PAYMENT_REQUIRED
        return null;
    }

    private CheckoutDraft? LoadDraft()
    {
        var json = HttpContext.Session.GetString(DraftKey);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<CheckoutDraft>(json);
    }

    private void SaveDraft(CheckoutDraft draft)
        => HttpContext.Session.SetString(DraftKey, JsonSerializer.Serialize(draft));
}
