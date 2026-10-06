using NewHarian.Domain.Enums;

namespace NewHarian.Application.Inventory;

public sealed record WarehouseLocationListItemDto(
    int Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    int LotCount = 0,
    int TotalQuantity = 0);

public class WarehouseLocationSaveRequest
{
    public int? Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record StockLotListItemDto(
    int Id,
    string Sku,
    string ProductName,
    string VariantLabel,
    string LocationCode,
    string LocationName,
    string? LotCode,
    DateOnly ExpiryDate,
    decimal UnitCost,
    int QuantityOnHand,
    decimal LineValue,
    DateTime ReceivedAt);

public class StockLotReceiveRequest
{
    public int ProductVariantId { get; set; }
    public int WarehouseLocationId { get; set; }
    public string? LotCode { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public decimal UnitCost { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

public class StockLotAdjustRequest
{
    public int LotId { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
}

public sealed record StockLotPickSuggestionDto(
    int StockLotId,
    string? LotCode,
    string LocationCode,
    string LocationName,
    DateOnly ExpiryDate,
    int AvailableQty,
    int AllocateQty);

public sealed record OrderItemPickPlanDto(
    int OrderItemId,
    string Sku,
    string ProductName,
    string VariantLabel,
    int RequestedQty,
    int AllocatedQty,
    int ShortfallQty,
    IReadOnlyList<StockLotPickSuggestionDto> Suggestions,
    int ExpiredQty = 0);

public sealed record OrderStockPickPlanDto(
    int OrderId,
    string OrderNumber,
    bool AlreadyDeducted,
    IReadOnlyList<OrderItemPickPlanDto> Lines);

public sealed record InventorySummaryDto(
    decimal TotalValue,
    int LowStockSkuCount,
    int LowStockThreshold,
    int ExpiringSoonLotCount,
    int ExpiringWithinDays,
    int ExpiredLotCount);

/// <summary>Filter for history: All; In = Receipt; Out = Issue.</summary>
public enum StockHistoryFilterKind
{
    All = 0,
    In = 1,
    Out = 2
}

public sealed record StockMovementListItemDto(
    long Id,
    DateTime CreatedAt,
    StockMovementType Type,
    string TypeLabelVi,
    string ProductName,
    string LocationLabel,
    int Quantity,
    string? Notes,
    string? ActorName);

public interface IInventoryService
{
    Task<IReadOnlyList<WarehouseLocationListItemDto>> ListLocationsAsync(CancellationToken ct = default);
    Task<WarehouseLocationSaveRequest?> GetLocationForEditAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SaveLocationAsync(WarehouseLocationSaveRequest request, CancellationToken ct = default);

    Task<(IReadOnlyList<StockLotListItemDto> Items, int Total)> ListLotsAsync(
        string? q,
        int? locationId,
        bool? expiringSoon,
        bool? outOfStock,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<IReadOnlyList<StockLotListItemDto>> ListLotsByLocationAsync(int locationId, CancellationToken ct = default);

    Task<StockLotListItemDto?> GetLotAsync(int lotId, CancellationToken ct = default);

    Task<(bool Ok, string? Error, int? LotId)> ReceiveLotAsync(
        StockLotReceiveRequest request,
        string? actorUserId,
        string? actorName,
        CancellationToken ct = default);

    Task<(bool Ok, string? Error)> AdjustLotAsync(
        StockLotAdjustRequest request,
        string? actorUserId,
        string? actorName,
        CancellationToken ct = default);

    Task<(IReadOnlyList<StockMovementListItemDto> Items, int Total)> ListMovementsAsync(
        StockHistoryFilterKind kind,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<InventorySummaryDto> GetSummaryAsync(CancellationToken ct = default);
}
