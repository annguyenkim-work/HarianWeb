using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Cart;

namespace NewHarian.Web.ViewComponents;

public class CartBadgeViewComponent(ICartService cart) : ViewComponent
{
    public IViewComponentResult Invoke()
        => View(cart.GetDistinctCount());
}
