using Microsoft.AspNetCore.SignalR;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Web.Authorization;

namespace NewHarian.Web.Areas.Admin.Hubs;

/// <summary>One SignalR group per notification permission; a connection joins the groups its user holds.
/// Role changes apply on the next connection (page load).</summary>
[HasPermission(Permissions.Notifications.View)]
public sealed class AdminNotificationsHub : Hub
{
    public static string GroupFor(string permission) => "perm:" + permission;

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user is not null)
        {
            foreach (var permission in AdminNotificationTypes.RequiredPermission.Values.Distinct())
            {
                if (user.HasPermission(permission))
                    await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(permission));
            }
        }
        await base.OnConnectedAsync();
    }
}
