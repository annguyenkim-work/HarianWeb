using NewHarian.Domain.Enums;

namespace NewHarian.Application.Orders;

public class CheckoutDraft
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public int ShippingProvinceId { get; set; }
    public string? ShippingDistrict { get; set; }
    public string? Notes { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
    public string LanguageCode { get; set; } = "vi";
    public string CheckoutId { get; set; } = Guid.NewGuid().ToString("N");
}

public record OrderLineDto(string ProductName, string VariantLabel, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderSummaryDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string ShippingAddress,
    string? ProvinceName,
    string? ShippingDistrict,
    string? Notes,
    string? InternalNotes,
    PaymentMethod PaymentMethod,
    OrderStatus Status,
    OrderSource Source,
    string? ExternalRef,
    decimal SubTotal,
    decimal ShippingFee,
    decimal Total,
    DateTime CreatedAt,
    IReadOnlyList<OrderLineDto> Items);

public record AdminOrderListItemDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    decimal Total,
    PaymentMethod PaymentMethod,
    OrderStatus Status,
    OrderSource Source,
    DateTime CreatedAt);

public class ManualOrderLineRequest
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
}

public class ManualOrderCreateRequest
{
    public OrderSource Source { get; set; } = OrderSource.Store;
    public string? ExternalRef { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public List<ManualOrderLineRequest> Lines { get; set; } = new() { new() };
}

public record OrderImportError(int? Row, string? OrderGroup, string Message);

public record OrderImportResult(
    int CreatedCount,
    IReadOnlyList<string> CreatedOrderNumbers,
    IReadOnlyList<OrderImportError> Errors);

public interface IOrderService
{
    Task<(bool Ok, string? Error, string? OrderNumber)> PlaceOrderAsync(CheckoutDraft draft, CancellationToken ct = default);
    Task<OrderSummaryDto?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<OrderSummaryDto?> TrackAsync(string orderNumber, string customerEmail, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> CancelGuestAsync(string orderNumber, string customerEmail, CancellationToken ct = default);
    Task<IReadOnlyList<AdminOrderListItemDto>> AdminListAsync(
        OrderStatus? status,
        PaymentMethod? payment,
        string? q,
        string? sort = null,
        string? dir = null,
        DateOnly? from = null,
        DateOnly? to = null,
        OrderSource? source = null,
        CancellationToken ct = default);
    Task<OrderSummaryDto?> AdminGetAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, string? OrderNumber)> CreateManualOrderAsync(ManualOrderCreateRequest request, CancellationToken ct = default);
    byte[] BuildOrderImportTemplate();
    Task<OrderImportResult> ImportOrdersAsync(Stream excelStream, CancellationToken ct = default);
    Task<byte[]> ExportOrdersCsvAsync(
        OrderStatus? status,
        PaymentMethod? payment,
        string? q,
        string? sort = null,
        string? dir = null,
        DateOnly? from = null,
        DateOnly? to = null,
        OrderSource? source = null,
        CancellationToken ct = default);
    Task<(bool Ok, string? Error)> AdminUpdateStatusAsync(int id, OrderStatus status, string? internalNotes, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> ConfirmCodAsync(int id, string? internalNotes, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> ConfirmBankTransferAsync(int id, string? internalNotes, CancellationToken ct = default);
}
