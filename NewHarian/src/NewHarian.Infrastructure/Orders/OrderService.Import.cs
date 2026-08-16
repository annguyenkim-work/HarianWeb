using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;

namespace NewHarian.Infrastructure.Orders;

public partial class OrderService
{
    private static readonly string[] ImportHeaders =
    [
        "OrderGroup", "Source", "ExternalRef", "CustomerName", "CitizenId", "CustomerPhone",
        "CustomerEmail", "ShippingAddress", "ProvinceCode", "CommuneCode", "Notes", "Sku", "Quantity", "UnitPrice"
    ];

    public byte[] BuildOrderImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Orders");
        for (var c = 0; c < ImportHeaders.Length; c++)
            ws.Cell(1, c + 1).Value = ImportHeaders[c];
        ws.Cell(2, 1).Value = "G1";
        ws.Cell(2, 2).Value = "Store";
        ws.Cell(2, 3).Value = "";
        ws.Cell(2, 4).Value = "Nguyen Van A";
        ws.Cell(2, 5).Value = "001234567890";
        ws.Cell(2, 6).Value = "0900000000";
        ws.Cell(2, 7).Value = "";
        ws.Cell(2, 8).Value = "12 Pho Hue";
        ws.Cell(2, 9).Value = "01";
        ws.Cell(2, 10).Value = "00004";
        ws.Cell(2, 11).Value = "";
        ws.Cell(2, 12).Value = "SKU-MAU";
        ws.Cell(2, 13).Value = 1;
        ws.Cell(2, 14).Value = "";
        ws.Cell(3, 1).Value = "G1";
        ws.Cell(3, 2).Value = "Store";
        ws.Cell(3, 4).Value = "Nguyen Van A";
        ws.Cell(3, 5).Value = "001234567890";
        ws.Cell(3, 6).Value = "0900000000";
        ws.Cell(3, 12).Value = "SKU-MAU-2";
        ws.Cell(3, 13).Value = 2;
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<OrderImportResult> ImportOrdersAsync(Stream excelStream, CancellationToken ct = default)
    {
        logger.LogInformation("ImportOrders Start");
        var errors = new List<OrderImportError>();
        var created = new List<string>();

        try
        {
            using var wb = new XLWorkbook(excelStream);
            var ws = wb.Worksheets.First();
            var headerRow = ws.Row(1);
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in headerRow.CellsUsed())
            {
                var name = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(name))
                    map[name] = cell.Address.ColumnNumber;
            }

            foreach (var required in new[] { "OrderGroup", "Source", "CustomerName", "CitizenId", "ShippingAddress", "ProvinceCode", "CommuneCode", "Sku", "Quantity" })
            {
                if (!map.ContainsKey(required))
                {
                    errors.Add(new OrderImportError(1, null, $"Thiếu cột bắt buộc '{required}'."));
                    logger.LogWarning("ImportOrders Done rejected missing column={Column}", required);
                    return new OrderImportResult(0, created, errors);
                }
            }

            var rows = new List<ImportRow>();
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastRow; r++)
            {
                string Cell(string col) =>
                    map.TryGetValue(col, out var c) ? ws.Cell(r, c).GetFormattedString().Trim() : string.Empty;

                var group = Cell("OrderGroup");
                var sku = Cell("Sku");
                if (string.IsNullOrWhiteSpace(group) && string.IsNullOrWhiteSpace(sku)
                    && string.IsNullOrWhiteSpace(Cell("CustomerName")))
                    continue;

                rows.Add(new ImportRow(
                    r,
                    group,
                    Cell("Source"),
                    NullIfEmpty(Cell("ExternalRef")),
                    Cell("CustomerName"),
                    Cell("CitizenId"),
                    NullIfEmpty(Cell("CustomerPhone")),
                    NullIfEmpty(Cell("CustomerEmail")),
                    NullIfEmpty(Cell("ShippingAddress")),
                    NullIfEmpty(Cell("ProvinceCode")),
                    NullIfEmpty(Cell("CommuneCode")),
                    NullIfEmpty(Cell("Notes")),
                    sku,
                    Cell("Quantity"),
                    NullIfEmpty(Cell("UnitPrice"))));
            }

            if (rows.Count == 0)
            {
                errors.Add(new OrderImportError(null, null, "File không có dòng dữ liệu."));
                logger.LogWarning("ImportOrders Done rejected Error=empty");
                return new OrderImportResult(0, created, errors);
            }

            foreach (var group in rows.GroupBy(x => x.OrderGroup, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(group.Key))
                {
                    foreach (var row in group)
                        errors.Add(new OrderImportError(row.Row, null, "OrderGroup bắt buộc."));
                    continue;
                }

                var list = group.ToList();
                var head = list[0];
                var groupErrors = new List<OrderImportError>();

                if (!TryParseManualSource(head.Source, out var source, out var sourceError))
                {
                    groupErrors.Add(new OrderImportError(head.Row, head.OrderGroup, sourceError!));
                }

                foreach (var row in list.Skip(1))
                {
                    if (!string.Equals(Norm(row.Source), Norm(head.Source), StringComparison.OrdinalIgnoreCase))
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "Source khác dòng đầu nhóm."));
                    if (!string.Equals(Norm(row.CustomerName), Norm(head.CustomerName), StringComparison.OrdinalIgnoreCase))
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "CustomerName khác dòng đầu nhóm."));
                    if (!string.Equals(Norm(row.CitizenId), Norm(head.CitizenId), StringComparison.OrdinalIgnoreCase))
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "CitizenId khác dòng đầu nhóm."));
                    if (!string.Equals(Norm(row.CustomerPhone), Norm(head.CustomerPhone), StringComparison.OrdinalIgnoreCase))
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "CustomerPhone khác dòng đầu nhóm."));
                    if (!string.Equals(Norm(row.CustomerEmail), Norm(head.CustomerEmail), StringComparison.OrdinalIgnoreCase))
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "CustomerEmail khác dòng đầu nhóm."));
                    if (!string.Equals(Norm(row.ShippingAddress), Norm(head.ShippingAddress), StringComparison.OrdinalIgnoreCase))
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "ShippingAddress khác dòng đầu nhóm."));
                    if (!string.Equals(Norm(row.ProvinceCode), Norm(head.ProvinceCode), StringComparison.OrdinalIgnoreCase))
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "ProvinceCode khác dòng đầu nhóm."));
                    if (!string.Equals(Norm(row.CommuneCode), Norm(head.CommuneCode), StringComparison.OrdinalIgnoreCase))
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "CommuneCode khác dòng đầu nhóm."));
                    if (!string.Equals(Norm(row.ExternalRef), Norm(head.ExternalRef), StringComparison.OrdinalIgnoreCase))
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "ExternalRef khác dòng đầu nhóm."));
                }

                var lines = new List<ManualOrderLineRequest>();
                foreach (var row in list)
                {
                    if (string.IsNullOrWhiteSpace(row.Sku))
                    {
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "Sku bắt buộc."));
                        continue;
                    }

                    if (!int.TryParse(row.Quantity, out var qty) || qty <= 0)
                    {
                        groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "Quantity phải là số nguyên > 0."));
                        continue;
                    }

                    decimal? unit = null;
                    if (!string.IsNullOrWhiteSpace(row.UnitPrice))
                    {
                        if (!decimal.TryParse(row.UnitPrice.Replace(",", "."), System.Globalization.NumberStyles.Number,
                                System.Globalization.CultureInfo.InvariantCulture, out var price) || price < 0)
                        {
                            groupErrors.Add(new OrderImportError(row.Row, row.OrderGroup, "UnitPrice không hợp lệ."));
                            continue;
                        }

                        unit = price;
                    }

                    lines.Add(new ManualOrderLineRequest { Sku = row.Sku, Quantity = qty, UnitPrice = unit });
                }

                if (groupErrors.Count > 0 || lines.Count == 0)
                {
                    if (lines.Count == 0 && groupErrors.Count == 0)
                        groupErrors.Add(new OrderImportError(head.Row, head.OrderGroup, "Nhóm không có dòng hàng hợp lệ."));
                    errors.AddRange(groupErrors);
                    continue;
                }

                var request = new ManualOrderCreateRequest
                {
                    Source = source,
                    Status = OrderStatus.Delivered,
                    ExternalRef = head.ExternalRef,
                    CustomerName = head.CustomerName,
                    CitizenId = head.CitizenId,
                    CustomerPhone = head.CustomerPhone,
                    CustomerEmail = head.CustomerEmail,
                    ShippingAddress = head.ShippingAddress,
                    ShippingProvinceCode = head.ProvinceCode,
                    ShippingCommuneCode = head.CommuneCode,
                    Notes = head.Notes,
                    Lines = lines
                };

                var (ok, error, orderNumber) = await CreateManualOrderAsync(request, ct);
                if (!ok)
                {
                    errors.Add(new OrderImportError(head.Row, head.OrderGroup, error ?? "Không tạo được đơn."));
                    continue;
                }

                created.Add(orderNumber!);
            }

            logger.LogInformation(
                "ImportOrders Done Created={Created} Errors={Errors}",
                created.Count, errors.Count);
            return new OrderImportResult(created.Count, created, errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ImportOrders Error");
            throw;
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string Norm(string? s) => (s ?? string.Empty).Trim();

    private static bool TryParseManualSource(string? raw, out OrderSource source, out string? error)
    {
        source = default;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Source bắt buộc.";
            return false;
        }

        var t = raw.Trim();
        if (t.Equals("Website", StringComparison.OrdinalIgnoreCase))
        {
            error = "Không import nguồn Website.";
            return false;
        }

        if (t.Equals("Cửa hàng", StringComparison.OrdinalIgnoreCase) || t.Equals("Cua hang", StringComparison.OrdinalIgnoreCase))
        {
            source = OrderSource.Store;
            return true;
        }

        if (Enum.TryParse(t, ignoreCase: true, out source)
            && source is OrderSource.Store or OrderSource.Shopee or OrderSource.TikTok)
            return true;

        error = $"Source '{raw}' không hợp lệ (Store / Shopee / TikTok).";
        return false;
    }

    private sealed record ImportRow(
        int Row,
        string OrderGroup,
        string Source,
        string? ExternalRef,
        string CustomerName,
        string CitizenId,
        string? CustomerPhone,
        string? CustomerEmail,
        string? ShippingAddress,
        string? ProvinceCode,
        string? CommuneCode,
        string? Notes,
        string Sku,
        string Quantity,
        string? UnitPrice);
}
