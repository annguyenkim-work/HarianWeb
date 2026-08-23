using NewHarian.Domain.Enums;

namespace NewHarian.Domain.Entities;

public class WarehouseLocation
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<StockLot> Lots { get; set; } = new List<StockLot>();
}

public class StockLot
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public int WarehouseLocationId { get; set; }
    public string? LotCode { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public decimal UnitCost { get; set; }
    public int QuantityOnHand { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;
    public WarehouseLocation WarehouseLocation { get; set; } = null!;
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
    public ICollection<OrderItemLotAllocation> Allocations { get; set; } = new List<OrderItemLotAllocation>();
}

public class StockMovement
{
    public long Id { get; set; }
    public int StockLotId { get; set; }
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public int? OrderId { get; set; }
    public int? OrderItemId { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StockLot StockLot { get; set; } = null!;
    public Order? Order { get; set; }
    public OrderItem? OrderItem { get; set; }
}

public class OrderItemLotAllocation
{
    public long Id { get; set; }
    public int OrderItemId { get; set; }
    public int StockLotId { get; set; }
    public int Quantity { get; set; }

    public OrderItem OrderItem { get; set; } = null!;
    public StockLot StockLot { get; set; } = null!;
}
