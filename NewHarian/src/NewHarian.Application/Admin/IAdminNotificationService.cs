using System.Security.Claims;
using NewHarian.Application.Abstractions;

namespace NewHarian.Application.Admin;

public static class AdminNotificationTypes
{
    public const string OrderCreated = "Order.Created";
    public const string OrderCancelledByGuest = "Order.CancelledByGuest";
    public const string ServiceBookingCreated = "ServiceBooking.Created";
    public const string InquiryCreated = "Inquiry.Created";
    public const string ApplicationCreated = "Application.Created";
    public const string DealerCreated = "Dealer.Created";

    /// <summary>Who sees each type (bell list + realtime push). Every type constant must be mapped.</summary>
    public static IReadOnlyDictionary<string, string> RequiredPermission { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [OrderCreated] = Permissions.Orders.View,
        [OrderCancelledByGuest] = Permissions.Orders.View,
        [ServiceBookingCreated] = Permissions.Bookings.View,
        [InquiryCreated] = Permissions.Inquiries.View,
        [ApplicationCreated] = Permissions.Applications.View,
        [DealerCreated] = Permissions.Dealers.View,
    };

    public static IReadOnlyList<string> VisibleTo(ClaimsPrincipal user)
        => RequiredPermission.Where(kv => user.HasPermission(kv.Value)).Select(kv => kv.Key).ToList();
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
    /// <summary>Push to connected admins holding <paramref name="permission"/>.</summary>
    Task NotifyAsync(AdminNotificationDto dto, string permission, CancellationToken ct = default);
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

    /// <param name="visibleTypes">Types the user may see (<see cref="AdminNotificationTypes.VisibleTo"/>).</param>
    Task<IReadOnlyList<AdminNotificationDto>> ListAsync(string userId, IReadOnlyCollection<string> visibleTypes, int take = 20, CancellationToken ct = default);
    Task<int> UnreadCountAsync(string userId, IReadOnlyCollection<string> visibleTypes, CancellationToken ct = default);
    Task MarkReadAsync(string userId, long notificationId, CancellationToken ct = default);
    Task MarkAllReadAsync(string userId, IReadOnlyCollection<string> visibleTypes, CancellationToken ct = default);
}
