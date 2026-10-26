using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Dashboard;
using NewHarian.Application.Inventory;
using NewHarian.Application.Orders;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Dashboard;

public sealed class AdminDashboardService(AppDbContext db, IInventoryService inventory) : IAdminDashboardService
{
    private const int MaxRangeDays = 366;
    private const int TopProductLimit = 10;

    private static readonly OrderStatus[] PendingOrderStatuses =
    [
        OrderStatus.PendingPayment,
        OrderStatus.AwaitingConfirmation
    ];

    /// <summary>Open pipeline for the "Đơn đang xử lý" commerce card (snapshot, all time).</summary>
    private static readonly OrderStatus[] InProgressOrderStatuses =
    [
        OrderStatus.PendingPayment,
        OrderStatus.AwaitingConfirmation,
        OrderStatus.Confirmed,
        OrderStatus.Processing
    ];

    private static readonly OrderStatus[] ConfirmedRevenueStatuses =
    [
        OrderStatus.Confirmed,
        OrderStatus.Processing,
        OrderStatus.Shipped,
        OrderStatus.Delivered
    ];

    private static readonly TimeZoneInfo LocalTz = ResolveLocalTz();

    public async Task<AdminDashboardDto> GetAsync(DateOnly start, DateOnly end, bool includeCharts, bool includeInventory, CancellationToken ct = default)
    {
        var range = NormalizeRange(start, end);
        var (utcFrom, utcToExclusive) = ToUtcBounds(range.Start, range.End);

        var pendingOrders = await db.Orders.AsNoTracking()
            .CountAsync(o => o.CreatedAt >= utcFrom && o.CreatedAt < utcToExclusive
                             && PendingOrderStatuses.Contains(o.Status), ct);

        var newBookings = await db.ServiceBookings.AsNoTracking()
            .CountAsync(b => b.CreatedAt >= utcFrom && b.CreatedAt < utcToExclusive
                             && b.Status == ServiceBookingStatus.New, ct);

        var newInquiries = await db.Inquiries.AsNoTracking()
            .CountAsync(i => i.CreatedAt >= utcFrom && i.CreatedAt < utcToExclusive
                             && i.Status == InquiryStatus.New, ct);

        var newApplications = await db.JobApplications.AsNoTracking()
            .CountAsync(a => a.CreatedAt >= utcFrom && a.CreatedAt < utcToExclusive
                             && a.Status == ApplicationStatus.New, ct);

        var kpis = new DashboardKpiDto(pendingOrders, newBookings, newInquiries, newApplications);
        InventoryDashboardDto? invDto = null;
        if (includeInventory)
        {
            var invSummary = await inventory.GetSummaryAsync(ct: ct);
            invDto = new InventoryDashboardDto(
                invSummary.TotalValue,
                invSummary.LowStockSkuCount,
                invSummary.LowStockThreshold,
                invSummary.ExpiringSoonLotCount,
                invSummary.ExpiringWithinDays,
                invSummary.ExpiredLotCount);
        }

        CommerceKpiDto? commerce = null;
        if (includeCharts)
            commerce = await BuildCommerceKpisAsync(range, ct);

        if (!includeCharts)
        {
            return new AdminDashboardDto { Range = range, Kpis = kpis, Inventory = invDto };
        }

        var allOrdersRaw = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= utcFrom && o.CreatedAt < utcToExclusive)
            .Select(o => new { o.CreatedAt, o.Total, o.Status, o.Source, o.PaymentMethod })
            .ToListAsync(ct);

        var orderCountByLocalDay = allOrdersRaw
            .GroupBy(x => ToLocalDate(x.CreatedAt))
            .ToDictionary(g => g.Key, g => g.Count());

        var revenueOrders = allOrdersRaw.Where(x => ConfirmedRevenueStatuses.Contains(x.Status)).ToList();
        var gmvByLocalDay = revenueOrders
            .GroupBy(x => ToLocalDate(x.CreatedAt))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

        var ordersTrendByDay = new List<DashboardDayTrendDto>();
        var gmvByDay = new List<DashboardDayPointDto>();
        for (var d = range.Start; d <= range.End; d = d.AddDays(1))
        {
            var rev = gmvByLocalDay.GetValueOrDefault(d);
            var cnt = orderCountByLocalDay.GetValueOrDefault(d);
            ordersTrendByDay.Add(new DashboardDayTrendDto(d, rev, cnt));
            gmvByDay.Add(new DashboardDayPointDto(d, rev));
        }

        var orderGmvTotal = revenueOrders.Sum(x => x.Total);

        var ordersByStatus = Enum.GetValues<OrderStatus>()
            .Select(s => new DashboardStatusCountDto(
                s.ToString(),
                LabelOrderStatus(s),
                allOrdersRaw.Count(x => x.Status == s)))
            .Where(x => x.Count > 0)
            .ToList();

        var ordersBySource = Enum.GetValues<OrderSource>()
            .Select(s => new DashboardStatusCountDto(
                s.ToString(),
                OrderSourceLabels.Vi(s),
                allOrdersRaw.Count(x => x.Source == s)))
            .Where(x => x.Count > 0)
            .ToList();

        var revenueBySource = Enum.GetValues<OrderSource>()
            .Select(s => new DashboardSourceAmountDto(
                s.ToString(),
                OrderSourceLabels.Vi(s),
                revenueOrders.Where(x => x.Source == s).Sum(x => x.Total)))
            .Where(x => x.Amount > 0)
            .ToList();
        var orderRevenueBySourceTotal = revenueBySource.Sum(x => x.Amount);

        var revenueByPayment = Enum.GetValues<PaymentMethod>()
            .Select(m => new DashboardSourceAmountDto(
                m.ToString(),
                PaymentMethodLabels.Vi(m),
                revenueOrders.Where(x => x.PaymentMethod == m).Sum(x => x.Total)))
            .Where(x => x.Amount > 0)
            .ToList();

        var topProducts = await LoadTopProductsAsync(utcFrom, utcToExclusive, ct);
        var revenueByCategory = await LoadRevenueByCategoryAsync(utcFrom, utcToExclusive, ct);

        var bookingStatusRaw = await db.ServiceBookings.AsNoTracking()
            .Where(b => b.CreatedAt >= utcFrom && b.CreatedAt < utcToExclusive)
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var bookingsByStatus = Enum.GetValues<ServiceBookingStatus>()
            .Select(s => new DashboardStatusCountDto(
                s.ToString(),
                LabelBookingStatus(s),
                bookingStatusRaw.FirstOrDefault(x => x.Status == s)?.Count ?? 0))
            .Where(x => x.Count > 0)
            .ToList();

        var bookingRaw = await db.ServiceBookings.AsNoTracking()
            .Where(b => b.CreatedAt >= utcFrom && b.CreatedAt < utcToExclusive
                        && b.Status != ServiceBookingStatus.Cancelled)
            .Select(b => new
            {
                b.CreatedAt,
                b.Status,
                Price = b.ServiceVariant != null ? b.ServiceVariant.Price : 0m
            })
            .ToListAsync(ct);

        var bookingByLocalDay = bookingRaw
            .GroupBy(x => ToLocalDate(x.CreatedAt))
            .ToDictionary(
                g => g.Key,
                g => (
                    Total: g.Count(),
                    Completed: g.Count(x => x.Status == ServiceBookingStatus.Completed),
                    Amount: g.Sum(x => x.Price)));

        var bookingsByDay = new List<DashboardDayCountDto>();
        var bookingGmvByDay = new List<DashboardDayPointDto>();
        for (var d = range.Start; d <= range.End; d = d.AddDays(1))
        {
            var v = bookingByLocalDay.GetValueOrDefault(d);
            bookingsByDay.Add(new DashboardDayCountDto(d, v.Total, v.Completed));
            bookingGmvByDay.Add(new DashboardDayPointDto(d, v.Amount));
        }

        var bookingGmvTotal = bookingRaw.Sum(x => x.Price);

        return new AdminDashboardDto
        {
            Range = range,
            Kpis = kpis,
            Commerce = commerce,
            OrdersTrendByDay = ordersTrendByDay,
            GmvByDay = gmvByDay,
            OrderGmvTotal = orderGmvTotal,
            TopProducts = topProducts,
            RevenueByCategory = revenueByCategory,
            RevenueByPaymentMethod = revenueByPayment,
            OrdersByStatus = ordersByStatus,
            OrdersBySource = ordersBySource,
            RevenueBySource = revenueBySource,
            OrderRevenueBySourceTotal = orderRevenueBySourceTotal,
            BookingsByStatus = bookingsByStatus,
            BookingsByDay = bookingsByDay,
            BookingGmvByDay = bookingGmvByDay,
            BookingGmvTotal = bookingGmvTotal,
            Inventory = invDto
        };
    }

    private async Task<CommerceKpiDto> BuildCommerceKpisAsync(DashboardDateRange range, CancellationToken ct)
    {
        var current = await SumCommerceAsync(range, ct);
        var previous = await SumCommerceAsync(DashboardKpiMath.PreviousPeriod(range), ct);
        var aov = DashboardKpiMath.AverageOrderValue(current.Revenue, current.RevenueOrderCount);
        var prevAov = DashboardKpiMath.AverageOrderValue(previous.Revenue, previous.RevenueOrderCount);
        var inProgress = await db.Orders.AsNoTracking()
            .CountAsync(o => InProgressOrderStatuses.Contains(o.Status), ct);
        return new CommerceKpiDto(
            current.Revenue,
            current.OrderCount,
            current.RevenueOrderCount,
            aov,
            inProgress,
            DashboardKpiMath.DeltaPercent(current.Revenue, previous.Revenue),
            DashboardKpiMath.DeltaPercent(current.OrderCount, previous.OrderCount),
            DashboardKpiMath.DeltaPercent(aov, prevAov));
    }

    private async Task<(decimal Revenue, int OrderCount, int RevenueOrderCount)> SumCommerceAsync(
        DashboardDateRange range, CancellationToken ct)
    {
        var (utcFrom, utcToExclusive) = ToUtcBounds(range.Start, range.End);
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= utcFrom && o.CreatedAt < utcToExclusive)
            .Select(o => new { o.Total, o.Status })
            .ToListAsync(ct);
        var orderCount = rows.Count;
        var revenueRows = rows.Where(x => ConfirmedRevenueStatuses.Contains(x.Status)).ToList();
        return (revenueRows.Sum(x => x.Total), orderCount, revenueRows.Count);
    }

    private async Task<IReadOnlyList<DashboardNamedAmountDto>> LoadTopProductsAsync(
        DateTime utcFrom, DateTime utcToExclusive, CancellationToken ct)
    {
        var rows = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order.CreatedAt >= utcFrom && i.Order.CreatedAt < utcToExclusive
                        && ConfirmedRevenueStatuses.Contains(i.Order.Status))
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                ProductName = g.Max(x => x.ProductName),
                Amount = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.Amount)
            .Take(TopProductLimit)
            .ToListAsync(ct);

        return rows
            .Select(x => new DashboardNamedAmountDto(
                x.ProductId.ToString(),
                string.IsNullOrWhiteSpace(x.ProductName) ? $"SP #{x.ProductId}" : x.ProductName!,
                x.Amount))
            .ToList();
    }

    private async Task<IReadOnlyList<DashboardNamedAmountDto>> LoadRevenueByCategoryAsync(
        DateTime utcFrom, DateTime utcToExclusive, CancellationToken ct)
    {
        var rows = await (
            from item in db.OrderItems.AsNoTracking()
            join product in db.Products.AsNoTracking() on item.ProductId equals product.Id into products
            from product in products.DefaultIfEmpty()
            join cat in db.Categories.AsNoTracking() on product.CategoryId equals cat.Id into cats
            from cat in cats.DefaultIfEmpty()
            join tr in db.CategoryTranslations.AsNoTracking().Where(t => t.LanguageCode == "vi")
                on cat.Id equals tr.CategoryId into trs
            from tr in trs.DefaultIfEmpty()
            where item.Order.CreatedAt >= utcFrom && item.Order.CreatedAt < utcToExclusive
                  && ConfirmedRevenueStatuses.Contains(item.Order.Status)
            group item by new
            {
                CategoryId = product != null ? product.CategoryId : (int?)null,
                Name = tr != null ? tr.Name : null
            }
            into g
            select new
            {
                g.Key.CategoryId,
                g.Key.Name,
                Amount = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(ct);

        return rows
            .Select(x => new DashboardNamedAmountDto(
                x.CategoryId?.ToString() ?? "none",
                string.IsNullOrWhiteSpace(x.Name) ? "Không danh mục" : x.Name!,
                x.Amount))
            .ToList();
    }

    public static DateOnly TodayLocal()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LocalTz));

    public static DashboardDateRange NormalizeRange(DateOnly? start, DateOnly? end)
        => NormalizeRange(start, end, TodayLocal());

    /// <summary>Defaults to the last 30 days ending <paramref name="today"/>; swaps reversed bounds; caps at 366 days.</summary>
    public static DashboardDateRange NormalizeRange(DateOnly? start, DateOnly? end, DateOnly today)
    {
        var defaultEnd = today;
        var defaultStart = today.AddDays(-29);

        var s = start ?? defaultStart;
        var e = end ?? defaultEnd;
        if (s > e) (s, e) = (e, s);

        var days = e.DayNumber - s.DayNumber + 1;
        if (days > MaxRangeDays)
            s = e.AddDays(-(MaxRangeDays - 1));

        return new DashboardDateRange(s, e);
    }

    private static DateOnly ToLocalDate(DateTime utc)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), LocalTz));

    private static (DateTime UtcFrom, DateTime UtcToExclusive) ToUtcBounds(DateOnly start, DateOnly end)
    {
        var localStart = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEndExclusive = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var utcFrom = TimeZoneInfo.ConvertTimeToUtc(localStart, LocalTz);
        var utcToExclusive = TimeZoneInfo.ConvertTimeToUtc(localEndExclusive, LocalTz);
        return (utcFrom, utcToExclusive);
    }

    private static TimeZoneInfo ResolveLocalTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException) { /* Windows */ }
        catch (InvalidTimeZoneException) { /* ignore */ }

        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }

    private static string LabelOrderStatus(OrderStatus s) => s switch
    {
        OrderStatus.PendingPayment => "Chờ thanh toán",
        OrderStatus.AwaitingConfirmation => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Processing => "Đang xử lý",
        OrderStatus.Shipped => "Đã gửi",
        OrderStatus.Delivered => "Đã giao",
        OrderStatus.Cancelled => "Đã hủy",
        OrderStatus.Refunded => "Hoàn tiền",
        _ => s.ToString()
    };

    private static string LabelBookingStatus(ServiceBookingStatus s) => s switch
    {
        ServiceBookingStatus.New => "Mới",
        ServiceBookingStatus.Confirmed => "Đã xác nhận",
        ServiceBookingStatus.Completed => "Hoàn thành",
        ServiceBookingStatus.Cancelled => "Đã hủy",
        _ => s.ToString()
    };
}
