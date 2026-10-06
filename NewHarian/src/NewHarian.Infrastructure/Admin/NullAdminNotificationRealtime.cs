using NewHarian.Application.Admin;

namespace NewHarian.Infrastructure.Admin;

/// <summary>Default no-op until Web registers SignalR broadcaster.</summary>
public sealed class NullAdminNotificationRealtime : IAdminNotificationRealtime
{
    public Task NotifyAsync(AdminNotificationDto dto, string permission, CancellationToken ct = default) => Task.CompletedTask;
}
