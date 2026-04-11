using Microsoft.AspNetCore.SignalR;

namespace NotifyHub.Api.Hubs;

public sealed class NotificationsHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }

    public async Task LeaveUserGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
    }
}
