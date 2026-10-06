using NewHarian.Domain.Enums;

namespace NewHarian.Application.Catalog;

/// <summary>New → Confirmed → Completed; cancel from New or Confirmed only.</summary>
public static class ServiceBookingStatusPolicy
{
    public static IReadOnlySet<ServiceBookingStatus> CancellableStatuses { get; } = new HashSet<ServiceBookingStatus>
    {
        ServiceBookingStatus.New,
        ServiceBookingStatus.Confirmed
    };

    public static bool CanTransition(ServiceBookingStatus from, ServiceBookingStatus to)
    {
        if (to == ServiceBookingStatus.Cancelled)
            return CancellableStatuses.Contains(from);

        return (from, to) switch
        {
            (ServiceBookingStatus.New, ServiceBookingStatus.Confirmed) => true,
            (ServiceBookingStatus.Confirmed, ServiceBookingStatus.Completed) => true,
            _ => false
        };
    }
}
