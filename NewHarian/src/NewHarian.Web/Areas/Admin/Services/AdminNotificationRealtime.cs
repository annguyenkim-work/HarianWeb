using Microsoft.AspNetCore.SignalR;
using NewHarian.Application.Admin;
using NewHarian.Web.Areas.Admin.Hubs;

namespace NewHarian.Web.Areas.Admin.Services;

public sealed class AdminNotificationRealtime(IHubContext<AdminNotificationsHub> hub) : IAdminNotificationRealtime
{
    public Task NotifyOpsAsync(AdminNotificationDto dto, CancellationToken ct = default)
        => hub.Clients.Group(AdminNotificationsHub.OpsGroup).SendAsync("notificationCreated", dto, ct);
}
