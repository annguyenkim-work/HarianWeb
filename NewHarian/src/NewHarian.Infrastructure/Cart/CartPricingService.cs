using NewHarian.Application.Cart;
using NewHarian.Application.DealerDiscounts;

namespace NewHarian.Infrastructure.Cart;

public sealed class CartPricingService(IDealerDiscountResolver discounts) : ICartPricingService
{
    public async Task<PricedCart> PriceAsync(CartDto cart, int? dealerId, CancellationToken ct = default)
    {
        var policy = dealerId is int id && cart.Items.Count > 0 ? await discounts.GetPolicyAsync(id, ct) : null;
        return CartPricing.Apply(cart, policy);
    }
}
