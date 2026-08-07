namespace NewHarian.Domain.Entities;

public class AdminNotification
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string Url { get; set; } = "/admin";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AdminNotificationRead> Reads { get; set; } = new List<AdminNotificationRead>();
}

public class AdminNotificationRead
{
    public long NotificationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    public AdminNotification Notification { get; set; } = null!;
}
