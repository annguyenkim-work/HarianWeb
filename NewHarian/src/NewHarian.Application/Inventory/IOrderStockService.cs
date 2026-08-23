namespace NewHarian.Application.Inventory;

public record StockPickSkuLineRequest(string Sku, int Quantity);

public interface IOrderStockService
{
    Task<OrderStockPickPlanDto?> PreviewPickPlanAsync(int orderId, CancellationToken ct = default);
    Task<OrderStockPickPlanDto?> GetAllocationsAsync(int orderId, CancellationToken ct = default);

    /// <summary>FEFO preview for Thêm đơn (before save) by SKU + qty.</summary>
    Task<OrderStockPickPlanDto> PreviewPickBySkusAsync(
        IReadOnlyList<StockPickSkuLineRequest> lines,
        CancellationToken ct = default);

    /// <summary>FEFO deduct when order reaches Processing or later (incl. manual Delivered). Idempotent via StockDeductedAt.</summary>
    Task<(bool Ok, string? Error, bool HadShortfall)> DeductForOrderAsync(
        int orderId,
        string? actorUserId,
        string? actorName,
        CancellationToken ct = default);

    Task<(bool Ok, string? Error)> RestoreForOrderAsync(
        int orderId,
        string? actorUserId,
        string? actorName,
        CancellationToken ct = default);
}
