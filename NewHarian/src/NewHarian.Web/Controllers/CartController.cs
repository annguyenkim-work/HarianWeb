using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using NewHarian.Application.Cart;
using NewHarian.Application.Dealers;
using NewHarian.Application.Formatting;
using NewHarian.Web.Models;

namespace NewHarian.Web.Controllers;

public class CartController(
    ICartService cart,
    ICartPricingService pricing,
    IDealerCheckoutContext dealerCheckout,
    IStringLocalizer<SharedResource> localizer) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var state = await dealerCheckout.GetAsync(ct);
        if (state.Expired) ViewBag.DealerNotice = localizer["Checkout.DealerCode.Expired"].Value;
        ViewBag.DealerBox = new DealerCodeBoxModel(
            state.Dealer, "/cart", TempData[CheckoutController.DealerCodeErrorKey] as string);
        return View(await pricing.PriceAsync(cart.GetCart(), state.Dealer?.Id, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Add(int productVariantId, int quantity = 1)
    {
        var (ok, error) = cart.Add(productVariantId, quantity);
        if (!ok)
            return BadRequest(new { ok = false, error });

        var snapshot = cart.GetCart();
        return Json(new
        {
            ok = true,
            count = snapshot.DistinctCount,
            subTotal = snapshot.SubTotal,
            subTotalText = MoneyFormat.Vnd(snapshot.SubTotal),
            cartUrl = Url.Action(nameof(Index), "Cart")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int productVariantId, int quantity, CancellationToken ct)
    {
        var (ok, error) = cart.Update(productVariantId, quantity);
        if (!ok)
            return BadRequest(new { ok = false, error });

        var snapshot = cart.GetCart();
        var line = snapshot.Items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
        var isDealer = (await dealerCheckout.GetAsync(ct)).Dealer is not null;
        return Json(new
        {
            ok = true,
            quantity = line?.Quantity ?? quantity,
            lineTotal = line?.LineTotal ?? 0,
            lineTotalText = MoneyFormat.Vnd(line?.LineTotal ?? 0),
            subTotal = snapshot.SubTotal,
            subTotalText = MoneyFormat.Vnd(snapshot.SubTotal),
            // Dealer tiers depend on the whole group quantity: the page re-renders every line's discount.
            reload = isDealer
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int productVariantId)
    {
        cart.Remove(productVariantId);
        return RedirectToAction(nameof(Index));
    }
}
