namespace NewHarian.Application.Dashboard;

public sealed record DashboardDateRange(DateOnly Start, DateOnly End);

public sealed record DashboardKpiDto(
    int PendingOrders,
    int NewBookings,
    int NewInquiries,
    int NewApplications);

/// <summary>Commerce scorecards for the selected range vs the previous equal-length period.</summary>
/// <param name="InProgressOrderCount">
/// Snapshot (not range-bound): orders still in the ops pipeline
/// (PendingPayment, AwaitingConfirmation, Confirmed, Processing).
/// </param>
public sealed record CommerceKpiDto(
    decimal Revenue,
    int OrderCount,
    int RevenueOrderCount,
    decimal Aov,
    int InProgressOrderCount,
    decimal? RevenueDeltaPct,
    decimal? OrderCountDeltaPct,
    decimal? AovDeltaPct);

public sealed record DashboardDayPointDto(DateOnly Date, decimal Amount);

public sealed record DashboardDayCountDto(DateOnly Date, int Total, int Completed);

/// <summary>Daily revenue (confirmed statuses) + all order creations for dual-axis trend.</summary>
public sealed record DashboardDayTrendDto(DateOnly Date, decimal Revenue, int OrderCount);

public sealed record DashboardStatusCountDto(string Key, string Label, int Count);

public sealed record DashboardSourceAmountDto(string Key, string Label, decimal Amount);

public sealed record DashboardNamedAmountDto(string Key, string Label, decimal Amount);

public sealed class AdminDashboardDto
{
    public required DashboardDateRange Range { get; init; }
    public required DashboardKpiDto Kpis { get; init; }
    public CommerceKpiDto? Commerce { get; init; }
    public IReadOnlyList<DashboardDayTrendDto>? OrdersTrendByDay { get; init; }
    public IReadOnlyList<DashboardDayPointDto>? GmvByDay { get; init; }
    public decimal OrderGmvTotal { get; init; }
    public IReadOnlyList<DashboardNamedAmountDto>? TopProducts { get; init; }
    public IReadOnlyList<DashboardNamedAmountDto>? RevenueByCategory { get; init; }
    public IReadOnlyList<DashboardSourceAmountDto>? RevenueByPaymentMethod { get; init; }
    public IReadOnlyList<DashboardStatusCountDto>? OrdersByStatus { get; init; }
    public IReadOnlyList<DashboardStatusCountDto>? OrdersBySource { get; init; }
    public IReadOnlyList<DashboardSourceAmountDto>? RevenueBySource { get; init; }
    public decimal OrderRevenueBySourceTotal { get; init; }
    public IReadOnlyList<DashboardStatusCountDto>? BookingsByStatus { get; init; }
    public IReadOnlyList<DashboardDayCountDto>? BookingsByDay { get; init; }
    public IReadOnlyList<DashboardDayPointDto>? BookingGmvByDay { get; init; }
    public decimal BookingGmvTotal { get; init; }
    public InventoryDashboardDto? Inventory { get; init; }
}

public sealed record InventoryDashboardDto(
    decimal TotalValue,
    int LowStockSkuCount,
    int LowStockThreshold,
    int ExpiringSoonLotCount,
    int ExpiringWithinDays,
    int ExpiredLotCount);

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetAsync(DateOnly start, DateOnly end, bool includeCharts, bool includeInventory, CancellationToken ct = default);
}
