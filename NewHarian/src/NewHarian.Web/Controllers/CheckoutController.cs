using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using NewHarian.Application.Address;
using NewHarian.Application.Cart;
using NewHarian.Application.Dealers;
using NewHarian.Application.Formatting;
using NewHarian.Application.Orders;
using NewHarian.Application.Payments;
using NewHarian.Application.Shipping;
using NewHarian.Application.Validation;
using NewHarian.Domain.Enums;
using NewHarian.Web.Models;
using NewHarian.Web.Security;

namespace NewHarian.Web.Controllers;

public class CheckoutController(
    ICartService cart,
    ICartPricingService pricing,
    IShippingService shipping,
    IOrderService orders,
    IBankTransferDisplayService bankTransfer,
    IVietnamDivisionCatalog catalog,
    IDealerAccessCodeService accessCodes,
    IDealerCheckoutContext dealerCheckout,
    IStringLocalizer<SharedResource> localizer) : Controller
{
    private const string DraftKey = "CheckoutDraft";
    private const string LastOrderKey = "LastPlacedOrder";
    public const string DealerCodeErrorKey = "DealerCodeError";

    private string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    [HttpGet("/checkout")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var basket = cart.GetCart();
        if (basket.Items.Count == 0) return RedirectToAction("Index", "Cart");

        var dealer = await LoadDealerAsync(ct);
        await SetSummaryAsync(basket, dealer, "/checkout", ct);

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

        var dealer = await LoadDealerAsync(ct);
        model.LanguageCode = Lang;
        var error = ValidateDraft(model, CitizenIdOptional(dealer));
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            await SetSummaryAsync(basket, dealer, "/checkout", ct);
            return View(model);
        }

        model.CheckoutId = Guid.NewGuid().ToString("N");
        SaveDraft(model);
        return RedirectToAction(nameof(Confirm));
    }

    [HttpGet("/checkout/shipping-fee")]
    public async Task<IActionResult> ShippingFee(string provinceCode, CancellationToken ct)
    {
        var dealer = (await dealerCheckout.GetAsync(ct)).Dealer;
        var priced = await pricing.PriceAsync(cart.GetCart(), dealer?.Id, ct);
        var (fee, isFree) = await shipping.CalculateFeeAsync(priced.NetSubTotal, provinceCode ?? "", ct);
        var total = priced.NetSubTotal + fee;
        return Json(new
        {
            fee,
            feeText = MoneyFormat.Vnd(fee),
            isFree,
            subTotal = priced.SubTotal,
            subTotalText = MoneyFormat.Vnd(priced.SubTotal),
            total,
            totalText = MoneyFormat.Vnd(total)
        });
    }

    [HttpGet("/checkout/confirm")]
    public async Task<IActionResult> Confirm(CancellationToken ct)
    {
        var draft = LoadDraft();
        var basket = cart.GetCart();
        if (draft is null || basket.Items.Count == 0) return RedirectToAction(nameof(Index));

        var dealer = await LoadDealerAsync(ct);
        var priced = await pricing.PriceAsync(basket, dealer?.Id, ct);
        var (fee, isFree) = await shipping.CalculateFeeAsync(priced.NetSubTotal, draft.ShippingProvinceCode, ct);
        catalog.TryResolve(draft.ShippingProvinceCode, draft.ShippingCommuneCode, out var addr);
        ViewBag.Priced = priced;
        ViewBag.Dealer = dealer;
        ViewBag.ShippingFee = fee;
        ViewBag.IsFreeShipping = isFree;
        ViewBag.ShipTo = AddressFormat.Join(draft.ShippingAddress, addr.CommuneName, addr.ProvinceName);
        ViewBag.Total = priced.NetSubTotal + fee;
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

        var state = await dealerCheckout.GetAsync(ct);
        if (state.Expired)
        {
            TempData["Error"] = localizer["Checkout.DealerCode.Expired"].Value;
            return RedirectToAction(nameof(Confirm));
        }

        var (ok, error, orderNumber) = await orders.PlaceOrderAsync(draft, state.Dealer?.Id, ct);
        if (!ok || orderNumber is null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Confirm));
        }

        HttpContext.Session.SetString(LastOrderKey, $"{draft.CheckoutId}|{orderNumber}");
        HttpContext.Session.Remove(DraftKey);
        dealerCheckout.Clear();
        return RedirectToAction(nameof(Success), new { orderNumber });
    }

    [HttpPost("/checkout/dealer-code")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitingSetup.DealerCodePolicy)]
    public async Task<IActionResult> DealerCode(string? code, string? returnUrl, CancellationToken ct)
    {
        var back = SafeReturnUrl(returnUrl);
        var identity = await accessCodes.VerifyAsync(code, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (identity is null)
        {
            var invalid = localizer["Checkout.DealerCode.Invalid"].Value;
            if (RateLimitingSetup.WantsJson(Request)) return Json(new { ok = false, error = invalid });
            TempData[DealerCodeErrorKey] = invalid;
            return LocalRedirect(back);
        }

        dealerCheckout.Set(identity);
        var dealer = (await dealerCheckout.GetAsync(ct)).Dealer;
        if (dealer is not null) PrefillDraft(dealer);
        return RateLimitingSetup.WantsJson(Request) ? Json(new { ok = true }) : LocalRedirect(back);
    }

    [HttpPost("/checkout/dealer-code/remove")]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveDealerCode(string? returnUrl)
    {
        dealerCheckout.Clear();
        return LocalRedirect(SafeReturnUrl(returnUrl));
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

    /// <summary>Dealer of this session; an outdated one is dropped with a notice on the page.</summary>
    private async Task<DealerOptionDto?> LoadDealerAsync(CancellationToken ct)
    {
        var state = await dealerCheckout.GetAsync(ct);
        if (state.Expired) ViewBag.DealerNotice = localizer["Checkout.DealerCode.Expired"].Value;
        return state.Dealer;
    }

    private async Task SetSummaryAsync(CartDto basket, DealerOptionDto? dealer, string returnUrl, CancellationToken ct)
    {
        ViewBag.Priced = await pricing.PriceAsync(basket, dealer?.Id, ct);
        ViewBag.Dealer = dealer;
        ViewBag.FreeThreshold = await shipping.GetFreeThresholdAsync(ct);
        ViewBag.DealerBox = new DealerCodeBoxModel(dealer, returnUrl, TempData[DealerCodeErrorKey] as string);
    }

    private void PrefillDraft(DealerOptionDto dealer)
    {
        var draft = LoadDraft() ?? new CheckoutDraft { PaymentMethod = PaymentMethod.COD, LanguageCode = Lang };
        draft.CustomerName = dealer.FullName;
        draft.CustomerEmail = dealer.Email;
        draft.CustomerPhone = dealer.Phone;
        draft.CitizenId = dealer.CitizenId ?? "";
        draft.ShippingAddress = dealer.ShipAddress;
        draft.ShippingProvinceCode = dealer.ShipProvinceCode ?? "";
        draft.ShippingCommuneCode = dealer.ShipCommuneCode ?? "";
        SaveDraft(draft);
    }

    private static bool CitizenIdOptional(DealerOptionDto? dealer) => dealer?.BusinessType == DealerBusinessType.Company;

    private string SafeReturnUrl(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/cart";

    private string? ValidateDraft(CheckoutDraft m, bool citizenIdOptional)
    {
        if (!GuestValidation.HasLength(m.CustomerName, 2, GuestValidation.NameMax))
            return "Vui lòng điền họ tên (2-200 ký tự)."; // CHK_REQUIRED
        if (!GuestValidation.IsEmail(m.CustomerEmail))
            return "Email không hợp lệ."; // CHK_EMAIL_INVALID
        if (string.IsNullOrWhiteSpace(m.CustomerPhone) || !GuestValidation.IsPhone(m.CustomerPhone))
            return "Số điện thoại không hợp lệ (8-20 ký tự, chỉ số/khoảng trắng/+/-)."; // CHK_PHONE_INVALID
        var skipCitizenId = citizenIdOptional && string.IsNullOrWhiteSpace(m.CitizenId);
        if (!skipCitizenId && !GuestValidation.IsCitizenId(m.CitizenId))
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
