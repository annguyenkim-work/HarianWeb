using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;

namespace NewHarian.Infrastructure.Orders;

public partial class OrderService
{
    public async Task<byte[]> ExportOrdersCsvAsync(
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
        var list = await AdminListAsync(status, payment, q, sort, dir, from, to, source, ct);
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

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 BOM for Excel
        sb.AppendLine(string.Join(',',
            "OrderNumber", "Source", "ExternalRef", "CustomerName", "Phone", "Email",
            "Status", "PaymentMethod", "Total", "CreatedAt", "Items"));

        // Need ExternalRef — not on AdminOrderListItemDto; load for ids
        var refs = await db.Orders.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, o.ExternalRef })
            .ToDictionaryAsync(o => o.Id, o => o.ExternalRef, ct);

        foreach (var o in list)
        {
            refs.TryGetValue(o.Id, out var ext);
            itemsMap.TryGetValue(o.Id, out var items);
            sb.Append(Csv(o.OrderNumber)).Append(',');
            sb.Append(Csv(OrderSourceLabels.Vi(o.Source))).Append(',');
            sb.Append(Csv(ext)).Append(',');
            sb.Append(Csv(o.CustomerName)).Append(',');
            sb.Append(Csv(o.CustomerPhone)).Append(',');
            sb.Append(Csv(o.CustomerEmail)).Append(',');
            sb.Append(Csv(o.Status.ToString())).Append(',');
            sb.Append(Csv(o.PaymentMethod.ToString())).Append(',');
            sb.Append(o.Total.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Csv(o.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Csv(items ?? string.Empty));
            sb.AppendLine();
        }

        logger.LogInformation("ExportOrders Done Count={Count}", list.Count);
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Csv(string? value)
    {
        var s = value ?? string.Empty;
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }
}
