using NewHarian.Application.Admin;

namespace NewHarian.Infrastructure.Admin;

/// <summary>Default no-op until Web registers SignalR broadcaster.</summary>
public sealed class NullAdminNotificationRealtime : IAdminNotificationRealtime
{
    public Task NotifyOpsAsync(AdminNotificationDto dto, CancellationToken ct = default) => Task.CompletedTask;
}
