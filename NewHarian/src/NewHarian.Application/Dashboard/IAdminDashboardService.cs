using NewHarian.Domain.Enums;

namespace NewHarian.Application.Dashboard;

public sealed record DashboardDateRange(DateOnly Start, DateOnly End);

public sealed record DashboardKpiDto(
    int PendingOrders,
    int NewBookings,
    int NewInquiries,
    int NewApplications);

public sealed record DashboardDayPointDto(DateOnly Date, decimal Amount);

public sealed record DashboardDayCountDto(DateOnly Date, int Total, int Completed);

public sealed record DashboardStatusCountDto(string Key, string Label, int Count);

public sealed class AdminDashboardDto
{
    public required DashboardDateRange Range { get; init; }
    public required DashboardKpiDto Kpis { get; init; }
    public IReadOnlyList<DashboardDayPointDto>? GmvByDay { get; init; }
    public decimal OrderGmvTotal { get; init; }
    public IReadOnlyList<DashboardStatusCountDto>? OrdersByStatus { get; init; }
    public IReadOnlyList<DashboardStatusCountDto>? BookingsByStatus { get; init; }
    public IReadOnlyList<DashboardDayCountDto>? BookingsByDay { get; init; }
    public IReadOnlyList<DashboardDayPointDto>? BookingGmvByDay { get; init; }
    public decimal BookingGmvTotal { get; init; }
}

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetAsync(DateOnly start, DateOnly end, bool includeCharts, CancellationToken ct = default);
}
