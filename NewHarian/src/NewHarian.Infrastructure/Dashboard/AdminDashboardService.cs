using Microsoft.EntityFrameworkCore;
using NewHarian.Application.Dashboard;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Dashboard;

public sealed class AdminDashboardService(AppDbContext db) : IAdminDashboardService
{
    private const int MaxRangeDays = 366;

    private static readonly OrderStatus[] PendingOrderStatuses =
    [
        OrderStatus.PendingPayment,
        OrderStatus.AwaitingConfirmation
    ];

    private static readonly OrderStatus[] ConfirmedRevenueStatuses =
    [
        OrderStatus.Confirmed,
        OrderStatus.Processing,
        OrderStatus.Shipped,
        OrderStatus.Delivered
    ];

    private static readonly TimeZoneInfo LocalTz = ResolveLocalTz();

    public async Task<AdminDashboardDto> GetAsync(DateOnly start, DateOnly end, bool includeCharts, CancellationToken ct = default)
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

        if (!includeCharts)
        {
            return new AdminDashboardDto { Range = range, Kpis = kpis };
        }

        var gmvRaw = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= utcFrom && o.CreatedAt < utcToExclusive
                        && ConfirmedRevenueStatuses.Contains(o.Status))
            .Select(o => new { o.CreatedAt, o.Total })
            .ToListAsync(ct);

        var gmvByLocalDay = gmvRaw
            .GroupBy(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc), LocalTz)))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

        var gmvByDay = new List<DashboardDayPointDto>();
        for (var d = range.Start; d <= range.End; d = d.AddDays(1))
            gmvByDay.Add(new DashboardDayPointDto(d, gmvByLocalDay.GetValueOrDefault(d)));
        var orderGmvTotal = gmvRaw.Sum(x => x.Total);

        var orderStatusRaw = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= utcFrom && o.CreatedAt < utcToExclusive)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var ordersByStatus = Enum.GetValues<OrderStatus>()
            .Select(s => new DashboardStatusCountDto(
                s.ToString(),
                LabelOrderStatus(s),
                orderStatusRaw.FirstOrDefault(x => x.Status == s)?.Count ?? 0))
            .Where(x => x.Count > 0)
            .ToList();

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
            .GroupBy(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc), LocalTz)))
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
            GmvByDay = gmvByDay,
            OrderGmvTotal = orderGmvTotal,
            OrdersByStatus = ordersByStatus,
            BookingsByStatus = bookingsByStatus,
            BookingsByDay = bookingsByDay,
            BookingGmvByDay = bookingGmvByDay,
            BookingGmvTotal = bookingGmvTotal
        };
    }

    public static DashboardDateRange NormalizeRange(DateOnly? start, DateOnly? end)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LocalTz));
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
