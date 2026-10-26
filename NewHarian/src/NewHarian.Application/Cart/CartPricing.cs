using NewHarian.Application.DealerDiscounts;
using NewHarian.Domain.Enums;

namespace NewHarian.Application.Cart;

public sealed record PricedCartLine(
    CartItemDto Item,
    decimal DiscountPercent,
    decimal DiscountAmount,
    string? DiscountTierLabel,
    DealerDiscountSource DiscountSource,
    bool Unconfigured)
{
    public decimal NetTotal => Item.LineTotal - DiscountAmount;
}

public sealed record PricedCart(IReadOnlyList<PricedCartLine> Lines, decimal SubTotal, decimal DiscountAmount, bool IsDealer)
{
    /// <summary>Amount actually paid for goods; also the base for the free-shipping threshold.</summary>
    public decimal NetSubTotal => SubTotal - DiscountAmount;
}

public static class CartPricing
{
    /// <summary>Guest cart (null policy) keeps list prices; a dealer cart gets the tiered dealer discount per line.</summary>
    public static PricedCart Apply(CartDto cart, DealerDiscountPolicy? policy)
    {
        if (policy is null)
        {
            var plain = cart.Items
                .Select(i => new PricedCartLine(i, 0, 0, null, DealerDiscountSource.None, false))
                .ToList();
            return new PricedCart(plain, cart.SubTotal, 0, false);
        }

        var discount = DealerDiscountCalculator.Calculate(
            policy,
            cart.Items.Select(i => new DiscountLineInput(i.ProductVariantId, i.Quantity, i.LineTotal)).ToList());
        var lines = cart.Items
            .Select((item, idx) =>
            {
                var d = discount.Lines[idx];
                return new PricedCartLine(item, d.Percent, d.Amount, d.TierLabel, d.Source, d.Unconfigured);
            })
            .ToList();
        return new PricedCart(lines, cart.SubTotal, discount.TotalDiscount, true);
    }
}

public interface ICartPricingService
{
    Task<PricedCart> PriceAsync(CartDto cart, int? dealerId, CancellationToken ct = default);
}
