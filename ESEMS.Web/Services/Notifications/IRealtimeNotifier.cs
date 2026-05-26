namespace ESEMS.Web.Services.Notifications;

public interface IRealtimeNotifier
{
    Task PushToUserAsync(int userId, string title, string message, string type = "Info", string? actionUrl = null);
    Task BroadcastDashboardRefreshAsync(string entityType);
}
