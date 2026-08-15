using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;

namespace NewHarian.Infrastructure.Orders;

public partial class OrderService
{
    public async Task<byte[]> ExportOrdersExcelAsync(
        OrderStatus? status,
        PaymentMethod? payment,
        string? q,
        string? sort = null,
        string? dir = null,
        DateOnly? from = null,
        DateOnly? to = null,
        OrderSource? source = null,
        CancellationToken ct = default)
    {
        var list = await BuildAdminOrderQuery(status, payment, q, sort, dir, from, to, source)
            .Select(o => new AdminOrderListItemDto(
                o.Id, o.OrderNumber, o.CustomerName, o.CustomerEmail, o.CustomerPhone,
                o.Total, o.PaymentMethod, o.Status, o.Source, o.CreatedAt))
            .ToListAsync(ct);
        var ids = list.Select(o => o.Id).ToList();
        var itemsByOrder = await db.OrderItems.AsNoTracking()
            .Where(i => ids.Contains(i.OrderId))
            .Select(i => new { i.OrderId, i.Sku, i.Quantity, i.ProductName, i.VariantLabel })
            .ToListAsync(ct);
        var itemsMap = itemsByOrder
            .GroupBy(i => i.OrderId)
            .ToDictionary(
                g => g.Key,
                g => string.Join("; ", g.Select(x =>
                {
                    var name = string.IsNullOrWhiteSpace(x.VariantLabel)
                        ? x.ProductName
                        : $"{x.ProductName} - {x.VariantLabel}";
                    return $"{x.Sku} x{x.Quantity} ({name})";
                })));

        var extra = await db.Orders.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, o.ExternalRef, o.CitizenId, o.DiscountAmount })
            .ToDictionaryAsync(o => o.Id, ct);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Orders");
        var headers = new[]
        {
            "OrderNumber", "Source", "ExternalRef", "CustomerName", "CitizenId", "Phone", "Email",
            "Status", "PaymentMethod", "DiscountAmount", "Total", "CreatedAt", "Items"
        };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var o in list)
        {
            extra.TryGetValue(o.Id, out var meta);
            itemsMap.TryGetValue(o.Id, out var items);
            ws.Cell(row, 1).Value = o.OrderNumber;
            ws.Cell(row, 2).Value = OrderSourceLabels.Vi(o.Source);
            ws.Cell(row, 3).Value = meta?.ExternalRef ?? string.Empty;
            ws.Cell(row, 4).Value = o.CustomerName;
            ws.Cell(row, 5).Value = meta?.CitizenId ?? string.Empty;
            ws.Cell(row, 6).Value = o.CustomerPhone ?? string.Empty;
            ws.Cell(row, 7).Value = o.CustomerEmail;
            ws.Cell(row, 8).Value = o.Status.ToString();
            ws.Cell(row, 9).Value = o.PaymentMethod.ToString();
            ws.Cell(row, 10).Value = meta?.DiscountAmount ?? 0;
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 11).Value = o.Total;
            ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 12).Value = o.CreatedAt.ToLocalTime();
            ws.Cell(row, 12).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            ws.Cell(row, 13).Value = items ?? string.Empty;
            row++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        logger.LogInformation("ExportOrders Done Count={Count} Format=xlsx", list.Count);
        return ms.ToArray();
    }
}
