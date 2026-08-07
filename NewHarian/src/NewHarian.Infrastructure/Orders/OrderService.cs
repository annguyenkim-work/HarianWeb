using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Cart;
using NewHarian.Application.Email;
using NewHarian.Application.Orders;
using NewHarian.Application.Shipping;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Orders;

public class OrderService(
    AppDbContext db,
    ICartService cart,
    IShippingService shipping,
    IEmailSender email,
    IEmailTemplateService emailTemplates,
    IAuditService audit,
    IStatusHistoryService history,
    IAdminNotificationService notifications,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<(bool Ok, string? Error, string? OrderNumber)> PlaceOrderAsync(CheckoutDraft draft, CancellationToken ct = default)
    {
        logger.LogInformation("PlaceOrder Start");
        try
        {
            var basket = cart.GetCart();
            if (basket.Items.Count == 0)
                return RejectPlaceOrder("Giỏ hàng trống.");

            if (string.IsNullOrWhiteSpace(draft.CustomerName) || string.IsNullOrWhiteSpace(draft.CustomerEmail)
                || string.IsNullOrWhiteSpace(draft.CustomerPhone) || string.IsNullOrWhiteSpace(draft.ShippingAddress))
                return RejectPlaceOrder("Vui lòng điền đầy đủ thông tin bắt buộc.");

            var province = await db.ShippingProvinces.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == draft.ShippingProvinceId && p.IsActive, ct);
            if (province is null)
                return RejectPlaceOrder("Vui lòng chọn Tỉnh/Thành phố.");

            if (draft.PaymentMethod is not (PaymentMethod.COD or PaymentMethod.BankTransfer))
                return RejectPlaceOrder("Phương thức thanh toán không hợp lệ.");

            var subTotal = basket.SubTotal;
            var (shipFee, _) = await shipping.CalculateFeeAsync(subTotal, draft.ShippingProvinceId, ct);
            var total = subTotal + shipFee;

            var orderNumber = await NextOrderNumberAsync(ct);
            var orderStatus = draft.PaymentMethod == PaymentMethod.COD
                ? OrderStatus.AwaitingConfirmation
                : OrderStatus.PendingPayment;

            var order = new Order
            {
                OrderNumber = orderNumber,
                CustomerName = draft.CustomerName.Trim(),
                CustomerEmail = draft.CustomerEmail.Trim(),
                CustomerPhone = draft.CustomerPhone.Trim(),
                ShippingAddress = draft.ShippingAddress.Trim(),
                ShippingProvinceId = draft.ShippingProvinceId,
                ShippingCity = province.NameVi,
                ShippingDistrict = draft.ShippingDistrict?.Trim(),
                Notes = draft.Notes?.Trim(),
                SubTotal = subTotal,
                ShippingFee = shipFee,
                Total = total,
                Status = orderStatus,
                PaymentMethod = draft.PaymentMethod,
                LanguageCode = string.IsNullOrWhiteSpace(draft.LanguageCode) ? "vi" : draft.LanguageCode,
                CreatedAt = DateTime.UtcNow,
                Payment = new Payment
                {
                    Method = draft.PaymentMethod,
                    Status = PaymentStatus.Pending,
                    Amount = total,
                    CreatedAt = DateTime.UtcNow
                }
            };

            foreach (var line in basket.Items)
            {
                order.Items.Add(new OrderItem
                {
                    ProductId = line.ProductId,
                    ProductVariantId = line.ProductVariantId,
                    ProductName = line.ProductName,
                    VariantLabel = line.VariantLabel,
                    Sku = line.Sku,
                    UnitPrice = line.UnitPrice,
                    Quantity = line.Quantity,
                    LineTotal = line.LineTotal
                });
            }

            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            cart.Clear();

            await history.AppendOrderAsync(
                order.Id,
                StatusHistoryEventTypes.Created,
                null,
                order.Status,
                actorIsGuest: true,
                guestActorName: order.CustomerEmail,
                ct);

            await notifications.PublishAsync(
                AdminNotificationTypes.OrderCreated,
                $"Đơn hàng mới {order.OrderNumber}",
                $"{order.CustomerName} · {order.Total:N0}đ",
                $"/admin/Orders?q={Uri.EscapeDataString(order.OrderNumber)}",
                "Order",
                order.Id.ToString(),
                ct);

            await SendOrderEmailsAsync(order, province.NameVi, ct);
            logger.LogInformation("PlaceOrder Done OrderNumber={OrderNumber}", order.OrderNumber);
            return (true, null, order.OrderNumber);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PlaceOrder Error");
            throw;
        }
    }

    private (bool Ok, string? Error, string? OrderNumber) RejectPlaceOrder(string error)
    {
        logger.LogWarning("PlaceOrder Done rejected Error={Error}", error);
        return (false, error, null);
    }

    public async Task<OrderSummaryDto?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        var o = await LoadOrderQuery().FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, ct);
        return o is null ? null : ToSummary(o);
    }

    public async Task<OrderSummaryDto?> TrackAsync(string orderNumber, string customerEmail, CancellationToken ct = default)
    {
        var emailNorm = customerEmail.Trim();
        var o = await LoadOrderQuery().FirstOrDefaultAsync(x =>
            x.OrderNumber == orderNumber.Trim() &&
            x.CustomerEmail.ToLower() == emailNorm.ToLower(), ct);
        return o is null ? null : ToSummary(o);
    }

    public async Task<(bool Ok, string? Error)> CancelGuestAsync(string orderNumber, string customerEmail, CancellationToken ct = default)
    {
        logger.LogInformation("CancelGuest Start OrderNumber={OrderNumber}", orderNumber);
        try
        {
            var o = await db.Orders.Include(x => x.Payment)
                .FirstOrDefaultAsync(x =>
                    x.OrderNumber == orderNumber.Trim() &&
                    x.CustomerEmail.ToLower() == customerEmail.Trim().ToLower(), ct);
            if (o is null)
            {
                logger.LogWarning("CancelGuest Done rejected OrderNumber={OrderNumber} Error={Error}", orderNumber, "Không tìm thấy đơn hàng.");
                return (false, "Không tìm thấy đơn hàng.");
            }
            if (o.Status != OrderStatus.PendingPayment || o.PaymentMethod != PaymentMethod.BankTransfer)
            {
                logger.LogWarning("CancelGuest Done rejected OrderNumber={OrderNumber} Error={Error}", orderNumber, "Không thể hủy đơn ở trạng thái hiện tại.");
                return (false, "Không thể hủy đơn ở trạng thái hiện tại.");
            }

            o.Status = OrderStatus.Cancelled;
            o.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(
                "Order.StatusChanged",
                "Order",
                o.Id.ToString(),
                new { Status = OrderStatus.PendingPayment.ToString() },
                new { Status = OrderStatus.Cancelled.ToString(), By = "Guest" },
                ct);

            await history.AppendOrderAsync(
                o.Id,
                StatusHistoryEventTypes.CancelledByGuest,
                OrderStatus.PendingPayment,
                OrderStatus.Cancelled,
                actorIsGuest: true,
                guestActorName: o.CustomerEmail,
                ct);

            await notifications.PublishAsync(
                AdminNotificationTypes.OrderCancelledByGuest,
                $"Khách hủy đơn {o.OrderNumber}",
                o.CustomerEmail,
                $"/admin/Orders?q={Uri.EscapeDataString(o.OrderNumber)}",
                "Order",
                o.Id.ToString(),
                ct);

            try
            {
                var staff = await GetSettingAsync("notifications.order_email")
                            ?? await GetSettingAsync("company.email")
                            ?? "info@harian.local";
                var (subject, body) = await emailTemplates.RenderAsync(EmailTemplateCodes.OrderCancelStaff, new Dictionary<string, string?>
                {
                    ["OrderNumber"] = EmailTemplateService.Enc(o.OrderNumber),
                    ["CustomerEmail"] = EmailTemplateService.Enc(o.CustomerEmail)
                }, ct);
                await email.SendAsync(staff, subject, body, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed cancel notify for {Order}", o.OrderNumber);
            }

            logger.LogInformation("CancelGuest Done OrderNumber={OrderNumber}", o.OrderNumber);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CancelGuest Error OrderNumber={OrderNumber}", orderNumber);
            throw;
        }
    }

    public async Task<IReadOnlyList<AdminOrderListItemDto>> AdminListAsync(
        OrderStatus? status,
        PaymentMethod? payment,
        string? q,
        string? sort = null,
        string? dir = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(o => o.Status == status);
        if (payment.HasValue) query = query.Where(o => o.PaymentMethod == payment);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.CustomerName.ToLower().Contains(term) ||
                o.CustomerEmail.ToLower().Contains(term) ||
                (o.CustomerPhone != null && o.CustomerPhone.ToLower().Contains(term)));
        }

        (from, to) = AdminListQuery.NormalizeDateRange(from, to);
        if (from is DateOnly fromDate)
        {
            var start = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(o => o.CreatedAt >= start);
        }
        if (to is DateOnly toDate)
        {
            var endExclusive = DateTime.SpecifyKind(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(o => o.CreatedAt < endExclusive);
        }

        var sortKey = AdminListQuery.NormalizeSort(sort, OrderSortKeys, "createdAt");
        var sortDir = AdminListQuery.NormalizeDir(dir, AdminListQuery.DefaultDirForColumn(sortKey));
        var asc = AdminListQuery.IsAsc(sortDir);

        query = (sortKey, asc) switch
        {
            ("orderNumber", true) => query.OrderBy(o => o.OrderNumber).ThenByDescending(o => o.Id),
            ("orderNumber", false) => query.OrderByDescending(o => o.OrderNumber).ThenByDescending(o => o.Id),
            ("customer", true) => query.OrderBy(o => o.CustomerName).ThenByDescending(o => o.Id),
            ("customer", false) => query.OrderByDescending(o => o.CustomerName).ThenByDescending(o => o.Id),
            ("total", true) => query.OrderBy(o => o.Total).ThenByDescending(o => o.Id),
            ("total", false) => query.OrderByDescending(o => o.Total).ThenByDescending(o => o.Id),
            ("payment", true) => query.OrderBy(o => o.PaymentMethod).ThenByDescending(o => o.Id),
            ("payment", false) => query.OrderByDescending(o => o.PaymentMethod).ThenByDescending(o => o.Id),
            ("status", true) => query.OrderBy(o => o.Status).ThenByDescending(o => o.Id),
            ("status", false) => query.OrderByDescending(o => o.Status).ThenByDescending(o => o.Id),
            ("createdAt", true) => query.OrderBy(o => o.CreatedAt).ThenByDescending(o => o.Id),
            _ => query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id),
        };

        var list = await query.ToListAsync(ct);
        return list.Select(o => new AdminOrderListItemDto(
            o.Id, o.OrderNumber, o.CustomerName, o.CustomerEmail, o.CustomerPhone,
            o.Total, o.PaymentMethod, o.Status, o.CreatedAt)).ToList();
    }

    private static readonly HashSet<string> OrderSortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "orderNumber", "customer", "total", "payment", "status", "createdAt"
    };

    public async Task<OrderSummaryDto?> AdminGetAsync(int id, CancellationToken ct = default)
    {
        var o = await LoadOrderQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is null ? null : ToSummary(o);
    }

    public async Task<(bool Ok, string? Error)> AdminUpdateStatusAsync(int id, OrderStatus status, string? internalNotes, CancellationToken ct = default)
    {
        logger.LogInformation("AdminUpdateStatus Start Id={Id} Status={Status}", id, status);
        try
        {
            var o = await db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (o is null)
            {
                logger.LogWarning("AdminUpdateStatus Done rejected Id={Id} Error={Error}", id, "Không tìm thấy.");
                return (false, "Không tìm thấy.");
            }
            if (!IsAllowedTransition(o.Status, status, o.PaymentMethod))
            {
                var msg = $"Không chuyển từ {o.Status} → {status}.";
                logger.LogWarning("AdminUpdateStatus Done rejected Id={Id} Error={Error}", id, msg);
                return (false, msg);
            }

            var from = o.Status;
            o.Status = status;
            if (internalNotes is not null) o.InternalNotes = internalNotes;
            o.UpdatedAt = DateTime.UtcNow;
            if (status == OrderStatus.Confirmed) o.ConfirmedAt ??= DateTime.UtcNow;
            if (status == OrderStatus.Shipped) o.ShippedAt ??= DateTime.UtcNow;
            if (status == OrderStatus.Delivered) o.DeliveredAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(
                "Order.StatusChanged",
                "Order",
                o.Id.ToString(),
                new { Status = from.ToString() },
                new { Status = status.ToString(), InternalNotes = internalNotes },
                ct);
            await history.AppendOrderAsync(
                o.Id,
                StatusHistoryEventTypes.StatusChanged,
                from,
                status,
                ct: ct);
            logger.LogInformation("AdminUpdateStatus Done Id={Id} Status={Status}", id, status);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AdminUpdateStatus Error Id={Id}", id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> ConfirmCodAsync(int id, string? internalNotes, CancellationToken ct = default)
    {
        logger.LogInformation("ConfirmCod Start Id={Id}", id);
        try
        {
            var o = await db.Orders.Include(x => x.Payment).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (o is null)
            {
                logger.LogWarning("ConfirmCod Done rejected Id={Id} Error={Error}", id, "Không tìm thấy.");
                return (false, "Không tìm thấy.");
            }
            if (o.PaymentMethod != PaymentMethod.COD || o.Status != OrderStatus.AwaitingConfirmation)
            {
                logger.LogWarning("ConfirmCod Done rejected Id={Id} Error={Error}", id, "Chỉ xác nhận COD khi đang chờ xác nhận.");
                return (false, "Chỉ xác nhận COD khi đang chờ xác nhận.");
            }

            o.Status = OrderStatus.Confirmed;
            o.ConfirmedAt = DateTime.UtcNow;
            o.UpdatedAt = DateTime.UtcNow;
            if (internalNotes is not null) o.InternalNotes = internalNotes;
            if (o.Payment is not null) o.Payment.Status = PaymentStatus.Pending;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(
                "Order.CodConfirmed",
                "Order",
                o.Id.ToString(),
                new { Status = OrderStatus.AwaitingConfirmation.ToString() },
                new { Status = OrderStatus.Confirmed.ToString(), InternalNotes = internalNotes },
                ct);
            await history.AppendOrderAsync(
                o.Id,
                StatusHistoryEventTypes.CodConfirmed,
                OrderStatus.AwaitingConfirmation,
                OrderStatus.Confirmed,
                ct: ct);
            logger.LogInformation("ConfirmCod Done Id={Id} OrderNumber={OrderNumber}", id, o.OrderNumber);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ConfirmCod Error Id={Id}", id);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> ConfirmBankTransferAsync(int id, string? internalNotes, CancellationToken ct = default)
    {
        logger.LogInformation("ConfirmBankTransfer Start Id={Id}", id);
        try
        {
            var o = await db.Orders.Include(x => x.Payment).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (o is null)
            {
                logger.LogWarning("ConfirmBankTransfer Done rejected Id={Id} Error={Error}", id, "Không tìm thấy.");
                return (false, "Không tìm thấy.");
            }
            if (o.PaymentMethod != PaymentMethod.BankTransfer || o.Status != OrderStatus.PendingPayment)
            {
                logger.LogWarning("ConfirmBankTransfer Done rejected Id={Id} Error={Error}", id, "Chỉ xác nhận CK khi đang chờ thanh toán.");
                return (false, "Chỉ xác nhận CK khi đang chờ thanh toán.");
            }

            o.Status = OrderStatus.Confirmed;
            o.ConfirmedAt = DateTime.UtcNow;
            o.UpdatedAt = DateTime.UtcNow;
            if (internalNotes is not null) o.InternalNotes = internalNotes;
            if (o.Payment is not null)
            {
                o.Payment.Status = PaymentStatus.Paid;
                o.Payment.PaidAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(
                "Order.PaymentConfirmed",
                "Order",
                o.Id.ToString(),
                new { Status = OrderStatus.PendingPayment.ToString() },
                new { Status = OrderStatus.Confirmed.ToString(), PaymentStatus = PaymentStatus.Paid.ToString(), InternalNotes = internalNotes },
                ct);
            await history.AppendOrderAsync(
                o.Id,
                StatusHistoryEventTypes.PaymentConfirmed,
                OrderStatus.PendingPayment,
                OrderStatus.Confirmed,
                ct: ct);
            logger.LogInformation("ConfirmBankTransfer Done Id={Id} OrderNumber={OrderNumber}", id, o.OrderNumber);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ConfirmBankTransfer Error Id={Id}", id);
            throw;
        }
    }

    private IQueryable<Order> LoadOrderQuery() =>
        db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.ShippingProvince)
            .Include(o => o.Payment);

    private static OrderSummaryDto ToSummary(Order o) => new(
        o.Id,
        o.OrderNumber,
        o.CustomerName,
        o.CustomerEmail,
        o.CustomerPhone,
        o.ShippingAddress,
        o.ShippingCity ?? o.ShippingProvince?.NameVi,
        o.ShippingDistrict,
        o.Notes,
        o.InternalNotes,
        o.PaymentMethod,
        o.Status,
        o.SubTotal,
        o.ShippingFee,
        o.Total,
        o.CreatedAt,
        o.Items.Select(i => new OrderLineDto(i.ProductName, i.VariantLabel, i.Sku, i.UnitPrice, i.Quantity, i.LineTotal)).ToList());

    private async Task<string> NextOrderNumberAsync(CancellationToken ct)
    {
        var prefix = PublicReferenceCodes.OrderPrefix;
        var existing = await db.Orders.AsNoTracking()
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .Select(o => o.OrderNumber)
            .ToListAsync(ct);
        return PublicReferenceCodes.Format(prefix, PublicReferenceCodes.NextSequence(existing, prefix));
    }

    private static bool IsAllowedTransition(OrderStatus from, OrderStatus to, PaymentMethod method)
    {
        if (to == OrderStatus.Cancelled)
            return from is OrderStatus.AwaitingConfirmation or OrderStatus.PendingPayment or OrderStatus.Confirmed or OrderStatus.Processing;

        return (from, to) switch
        {
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            _ => false
        };
    }

    private async Task SendOrderEmailsAsync(Order order, string provinceName, CancellationToken ct)
    {
        var staff = await GetSettingAsync("notifications.order_email")
                    ?? await GetSettingAsync("company.email")
                    ?? "info@harian.local";

        var lines = string.Join("<br/>", order.Items.Select(i =>
            $"{EmailTemplateService.Enc(i.ProductName)} ({EmailTemplateService.Enc(i.VariantLabel)}) × {i.Quantity} = {i.LineTotal:N0}đ"));

        var bankBlock = "";
        if (order.PaymentMethod == PaymentMethod.BankTransfer)
        {
            var bankName = await GetSettingAsync("company.bank.name");
            var bankAcc = await GetSettingAsync("company.bank.account");
            var bankBranch = await GetSettingAsync("company.bank.branch");
            var bankQr = await GetSettingAsync("company.bank.qr");
            bankBlock =
                "<hr/><p><strong>Thông tin chuyển khoản</strong></p>" +
                $"<p>Ngân hàng: {EmailTemplateService.Enc(bankName)}<br/>STK: {EmailTemplateService.Enc(bankAcc)}<br/>Chi nhánh: {EmailTemplateService.Enc(bankBranch)}<br/>" +
                $"Số tiền: <strong>{order.Total:N0}đ</strong><br/>Nội dung: <strong>{EmailTemplateService.Enc(order.OrderNumber)}</strong></p>";
            if (!string.IsNullOrWhiteSpace(bankQr))
                bankBlock += $"<p><img src=\"{EmailTemplateService.Enc(bankQr)}\" alt=\"QR chuyển khoản\" style=\"max-width:240px;height:auto;margin-top:8px\" /></p>";
        }

        var vars = new Dictionary<string, string?>
        {
            ["OrderNumber"] = EmailTemplateService.Enc(order.OrderNumber),
            ["CustomerName"] = EmailTemplateService.Enc(order.CustomerName),
            ["CustomerEmail"] = EmailTemplateService.Enc(order.CustomerEmail),
            ["CustomerPhone"] = EmailTemplateService.Enc(order.CustomerPhone),
            ["ShippingAddress"] = EmailTemplateService.Enc(order.ShippingAddress),
            ["ProvinceName"] = EmailTemplateService.Enc(provinceName),
            ["PaymentMethod"] = EmailTemplateService.Enc(order.PaymentMethod.ToString()),
            ["OrderLinesHtml"] = lines,
            ["SubTotal"] = order.SubTotal.ToString("N0"),
            ["ShippingFee"] = order.ShippingFee.ToString("N0"),
            ["Total"] = order.Total.ToString("N0"),
            ["BankBlockHtml"] = bankBlock
        };

        try
        {
            var staffMail = await emailTemplates.RenderAsync(EmailTemplateCodes.OrderStaff, vars, ct);
            await email.SendAsync(staff, staffMail.Subject, staffMail.Body, ct);
            var customerMail = await emailTemplates.RenderAsync(EmailTemplateCodes.OrderCustomer, vars, ct);
            await email.SendAsync(order.CustomerEmail, customerMail.Subject, customerMail.Body, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Order email failed for {Order}", order.OrderNumber);
        }
    }

    private async Task<string?> GetSettingAsync(string key)
        => await db.SiteSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();
}
