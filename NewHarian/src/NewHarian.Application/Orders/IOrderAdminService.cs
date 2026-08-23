using NewHarian.Domain.Enums;

namespace NewHarian.Application.Orders;

public interface IOrderAdminService
{
    Task<(IReadOnlyList<AdminOrderListItemDto> Items, int Total)> AdminListAsync(
        OrderStatus? status,
        PaymentMethod? payment,
        string? q,
        string? sort = null,
        string? dir = null,
        DateOnly? from = null,
        DateOnly? to = null,
        OrderSource? source = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task<OrderSummaryDto?> AdminGetAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, string? OrderNumber)> CreateManualOrderAsync(
        ManualOrderCreateRequest request,
        string? actorUserId = null,
        string? actorName = null,
        CancellationToken ct = default);
    Task<(bool Ok, string? Error)> AdminUpdateStatusAsync(
        int id,
        OrderStatus status,
        string? internalNotes,
        string? actorUserId = null,
        string? actorName = null,
        CancellationToken ct = default);
    Task<(bool Ok, string? Error)> ConfirmCodAsync(
        int id,
        string? internalNotes,
        CancellationToken ct = default);
    Task<(bool Ok, string? Error)> ConfirmBankTransferAsync(
        int id,
        string? internalNotes,
        CancellationToken ct = default);
}
