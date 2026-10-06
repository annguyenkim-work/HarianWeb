using Microsoft.AspNetCore.SignalR;
using NewHarian.Application.Admin;
using NewHarian.Web.Areas.Admin.Hubs;

namespace NewHarian.Web.Areas.Admin.Services;

public sealed class AdminNotificationRealtime(IHubContext<AdminNotificationsHub> hub) : IAdminNotificationRealtime
{
    public Task NotifyAsync(AdminNotificationDto dto, string permission, CancellationToken ct = default)
        => hub.Clients.Group(AdminNotificationsHub.GroupFor(permission)).SendAsync("notificationCreated", dto, ct);
}
