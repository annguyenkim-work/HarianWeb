using NewHarian.Domain.Enums;

namespace NewHarian.Application.Orders;

public record ManualOrderPricedLine(
    int Index,
    string Sku,
    string? Error,
    int ProductId,
    int ProductVariantId,
    string ProductName,
    string VariantLabel,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    string? DiscountTierLabel,
    DealerDiscountSource DiscountSource,
    bool DiscountUnconfigured,
    decimal PolicyPercent);

public record ManualOrderPricingResult(
    IReadOnlyList<ManualOrderPricedLine> Lines,
    decimal SubTotal,
    decimal DiscountAmount)
{
    /// <summary>First line error (blocks saving) or null.</summary>
    public string? Error => Lines.FirstOrDefault(l => l.Error is not null)?.Error;
    public bool HasManualDiscount => Lines.Any(l => l.DiscountSource == DealerDiscountSource.Manual);
}

public class DealerDiscountPreviewRequest
{
    public int? DealerId { get; set; }
    public List<ManualOrderLineRequest> Lines { get; set; } = new();
}

/// <summary>
/// Resolves manual-order lines by SKU and applies the dealer discount table.
/// Shared by order creation and the live preview so both compute the same numbers.
/// </summary>
public interface IManualOrderPricingService
{
    /// <param name="dealerId">Approved dealer, or null for a retail order (no discount).</param>
    /// <param name="allowManualDiscount">Caller holds Orders.OverrideDiscount; otherwise line percents are ignored.</param>
    Task<ManualOrderPricingResult> PriceAsync(
        int? dealerId,
        IReadOnlyList<ManualOrderLineRequest> lines,
        bool allowManualDiscount,
        CancellationToken ct = default);
}
