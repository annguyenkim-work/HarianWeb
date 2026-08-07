using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NewHarian.Application.Abstractions;

namespace NewHarian.Web.Areas.Admin.Hubs;

[Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
public sealed class AdminNotificationsHub : Hub
{
    public const string OpsGroup = "ops";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, OpsGroup);
        await base.OnConnectedAsync();
    }
}
