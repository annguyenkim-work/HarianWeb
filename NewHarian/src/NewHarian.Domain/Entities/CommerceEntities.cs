using NewHarian.Domain.Enums;

namespace NewHarian.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public int? ShippingProvinceId { get; set; }
    public string? ShippingCity { get; set; }
    public string? ShippingDistrict { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public decimal SubTotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public OrderSource Source { get; set; } = OrderSource.Website;
    public string? ExternalRef { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public ShippingProvince? ShippingProvince { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
    public ICollection<OrderHistory> Histories { get; set; } = new List<OrderHistory>();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }

    public Order Order { get; set; } = null!;
}

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? GatewayName { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? GatewayResponseJson { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public Order Order { get; set; } = null!;
}

public class ShippingProvince
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameVi { get; set; } = string.Empty;
    public string? NameJa { get; set; }
    public string? NameEn { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ShippingRate? Rate { get; set; }
}

public class ShippingRate
{
    public int Id { get; set; }
    public int ProvinceId { get; set; }
    public decimal Fee { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ShippingProvince Province { get; set; } = null!;
}
