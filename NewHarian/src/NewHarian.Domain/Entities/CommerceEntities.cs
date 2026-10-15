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
    public string? CitizenId { get; set; }
    public int? DealerId { get; set; }
    /// <summary>Legacy single % for the whole order; new dealer orders store % per line.</summary>
    public decimal? DealerDiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? InvoiceBuyerName { get; set; }
    public string? InvoiceTaxCode { get; set; }
    public string? InvoiceAddress { get; set; }
    public string? InvoiceEmail { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public int? ShippingProvinceId { get; set; }
    public string? ShippingCity { get; set; }
    public string? ShippingDistrict { get; set; }
    public string? ShippingCommuneCode { get; set; }
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
    /// <summary>Set when stock was deducted (Processing); cleared on restore.</summary>
    public DateTime? StockDeductedAt { get; set; }

    public ShippingProvince? ShippingProvince { get; set; }
    public Dealer? Dealer { get; set; }
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
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountTierLabel { get; set; }
    public DealerDiscountSource DiscountSource { get; set; } = DealerDiscountSource.None;

    public Order Order { get; set; } = null!;
    public ICollection<OrderItemLotAllocation> LotAllocations { get; set; } = new List<OrderItemLotAllocation>();
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

public class Dealer
{
    public int Id { get; set; }
    public DealerBusinessType BusinessType { get; set; } = DealerBusinessType.Company;
    public string BusinessName { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string? HouseholdRegistrationNo { get; set; }
    public string RepresentativeName { get; set; } = string.Empty;
    public string? RepresentativeTitle { get; set; }
    /// <summary>Contact person.</summary>
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Head office (as registered) — used on VAT invoices.</summary>
    public string Address { get; set; } = string.Empty;
    public string? ProvinceCode { get; set; }
    public string? ProvinceName { get; set; }
    public string? CommuneCode { get; set; }
    public string? CommuneName { get; set; }
    public bool ShipToHeadOffice { get; set; } = true;
    public string? ShippingAddress { get; set; }
    public string? ShippingProvinceCode { get; set; }
    public string? ShippingProvinceName { get; set; }
    public string? ShippingCommuneCode { get; set; }
    public string? ShippingCommuneName { get; set; }
    public string? InvoiceEmail { get; set; }
    public int? BusinessLicenseMediaFileId { get; set; }
    public string? CitizenId { get; set; }
    public string? Message { get; set; }
    public DealerStatus Status { get; set; } = DealerStatus.Pending;
    public string? InternalNotes { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    /// <summary>HMAC of the normalized access code; the plain code is only ever sent by email.</summary>
    public string? AccessCodeHash { get; set; }
    public string? AccessCodeLast4 { get; set; }
    /// <summary>Also stamps checkout sessions: a re-issued code invalidates sessions verified with the old one.</summary>
    public DateTime? AccessCodeIssuedAt { get; set; }
    public string? AccessCodeIssuedByUserId { get; set; }

    public MediaFile? BusinessLicense { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<DealerDiscountOverride> DiscountOverrides { get; set; } = new List<DealerDiscountOverride>();
}
