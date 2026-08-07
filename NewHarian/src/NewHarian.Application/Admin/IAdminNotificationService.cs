namespace NewHarian.Application.Admin;

public static class AdminNotificationTypes
{
    public const string OrderCreated = "Order.Created";
    public const string OrderCancelledByGuest = "Order.CancelledByGuest";
    public const string ServiceBookingCreated = "ServiceBooking.Created";
    public const string InquiryCreated = "Inquiry.Created";
    public const string ApplicationCreated = "Application.Created";
}

public record AdminNotificationDto(
    long Id,
    string Type,
    string Title,
    string? Body,
    string Url,
    DateTime CreatedAt,
    string? EntityType,
    string? EntityId,
    bool IsRead);

public interface IAdminNotificationRealtime
{
    Task NotifyOpsAsync(AdminNotificationDto dto, CancellationToken ct = default);
}

public interface IAdminNotificationService
{
    Task PublishAsync(
        string type,
        string title,
        string? body,
        string url,
        string? entityType,
        string? entityId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdminNotificationDto>> ListAsync(string userId, int take = 20, CancellationToken ct = default);
    Task<int> UnreadCountAsync(string userId, CancellationToken ct = default);
    Task MarkReadAsync(string userId, long notificationId, CancellationToken ct = default);
    Task MarkAllReadAsync(string userId, CancellationToken ct = default);
}
