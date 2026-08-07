using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Cart;

namespace NewHarian.Web.ViewComponents;

public class CartBadgeViewComponent(ICartService cart) : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var c = cart.GetCart();
        return View(c.DistinctCount);
    }
}
