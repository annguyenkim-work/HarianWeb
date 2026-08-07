using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Cart;

namespace NewHarian.Web.Controllers;

public class CartController(ICartService cart) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(cart.GetCart());

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
            subTotalText = snapshot.SubTotal.ToString("N0") + "đ",
            cartUrl = Url.Action(nameof(Index), "Cart")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update(int productVariantId, int quantity)
    {
        var (ok, error) = cart.Update(productVariantId, quantity);
        if (!ok)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int productVariantId)
    {
        cart.Remove(productVariantId);
        return RedirectToAction(nameof(Index));
    }
}
