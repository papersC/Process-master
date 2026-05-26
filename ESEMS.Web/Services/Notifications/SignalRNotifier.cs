using Microsoft.AspNetCore.SignalR;
using ESEMS.Web.Hubs;

namespace ESEMS.Web.Services.Notifications;

public class SignalRNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotifier(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public async Task PushToUserAsync(int userId, string title, string message, string type = "Info", string? actionUrl = null)
    {
        await _hub.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", new
        {
            title,
            message,
            type,
            actionUrl,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastDashboardRefreshAsync(string entityType)
    {
        await _hub.Clients.Group("all").SendAsync("DashboardRefresh", new
        {
            entityType,
            timestamp = DateTime.UtcNow
        });
    }
}
