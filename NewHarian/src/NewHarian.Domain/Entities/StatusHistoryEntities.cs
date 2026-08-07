namespace NewHarian.Domain.Entities;

public class OrderHistory
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? FromStatus { get; set; }
    public int? ToStatus { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string MessageVi { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
}

public class ServiceBookingHistory
{
    public long Id { get; set; }
    public int BookingId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? FromStatus { get; set; }
    public int? ToStatus { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string MessageVi { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ServiceBooking Booking { get; set; } = null!;
}
