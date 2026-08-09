using NewHarian.Domain.Enums;

namespace NewHarian.Application.Admin;

public static class StatusHistoryEventTypes
{
    public const string Created = "Created";
    public const string ManualCreated = "ManualCreated";
    public const string CodConfirmed = "CodConfirmed";
    public const string PaymentConfirmed = "PaymentConfirmed";
    public const string StatusChanged = "StatusChanged";
    public const string CancelledByGuest = "CancelledByGuest";
}

public static class StatusHistoryMessages
{
    public static string ForOrder(string eventType, OrderStatus? toStatus)
    {
        if (eventType == StatusHistoryEventTypes.Created)
            return "Đơn hàng được tạo";
        if (eventType == StatusHistoryEventTypes.ManualCreated)
            return "Tạo đơn thủ công";
        if (eventType == StatusHistoryEventTypes.CodConfirmed)
            return "Tiếp nhận đơn (xác nhận COD)";
        if (eventType == StatusHistoryEventTypes.PaymentConfirmed)
            return "Tiếp nhận đơn (xác nhận chuyển khoản)";
        if (eventType == StatusHistoryEventTypes.CancelledByGuest)
            return "Khách hủy đơn";

        return toStatus switch
        {
            OrderStatus.Confirmed => "Tiếp nhận đơn",
            OrderStatus.Processing => "Bắt đầu xử lý / đóng gói",
            OrderStatus.Shipped => "Gửi đơn đi (giao vận)",
            OrderStatus.Delivered => "Hoàn thành giao hàng",
            OrderStatus.Cancelled => "Hủy đơn",
            _ => toStatus.HasValue ? $"Trạng thái → {toStatus}" : "Cập nhật trạng thái"
        };
    }

    public static string ForBooking(string eventType, ServiceBookingStatus? toStatus)
    {
        if (eventType == StatusHistoryEventTypes.Created)
            return "Đặt lịch được tạo";

        return toStatus switch
        {
            ServiceBookingStatus.Confirmed => "Tiếp nhận lịch hẹn",
            ServiceBookingStatus.Completed => "Hoàn thành dịch vụ",
            ServiceBookingStatus.Cancelled => "Hủy lịch hẹn",
            _ => toStatus.HasValue ? $"Trạng thái → {toStatus}" : "Cập nhật trạng thái"
        };
    }
}

public record StatusHistoryItemDto(
    long Id,
    DateTime CreatedAt,
    string EventType,
    string ActorType,
    string? ActorName,
    string MessageVi,
    int? FromStatus,
    int? ToStatus);

public interface IStatusHistoryService
{
    Task AppendOrderAsync(
        int orderId,
        string eventType,
        OrderStatus? fromStatus,
        OrderStatus? toStatus,
        bool actorIsGuest = false,
        string? guestActorName = null,
        string? messageVi = null,
        CancellationToken ct = default);

    Task AppendBookingAsync(
        int bookingId,
        string eventType,
        ServiceBookingStatus? fromStatus,
        ServiceBookingStatus? toStatus,
        bool actorIsGuest = false,
        string? guestActorName = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<StatusHistoryItemDto>> ListForOrderAsync(int orderId, CancellationToken ct = default);
    Task<IReadOnlyList<StatusHistoryItemDto>> ListForBookingAsync(int bookingId, CancellationToken ct = default);
}
