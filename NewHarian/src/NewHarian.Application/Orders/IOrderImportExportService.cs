using NewHarian.Domain.Enums;

namespace NewHarian.Application.Orders;

public interface IOrderImportExportService
{
    byte[] BuildOrderImportTemplate();
    Task<OrderImportResult> ImportOrdersAsync(
        Stream excelStream,
        string? actorUserId = null,
        string? actorName = null,
        CancellationToken ct = default);
    Task<byte[]> ExportOrdersExcelAsync(
        OrderStatus? status,
        PaymentMethod? payment,
        string? q,
        string? sort = null,
        string? dir = null,
        DateOnly? from = null,
        DateOnly? to = null,
        OrderSource? source = null,
        CancellationToken ct = default);
}
