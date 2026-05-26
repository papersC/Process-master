using ESEMS.Web.Models.Notifications;

namespace ESEMS.Web.Services.Notifications;

public interface INotificationService
{
    Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false, int take = 20);
    Task<int> GetUnreadCountAsync(int userId);
    Task SendAsync(int userId, string titleEn, string titleAr, string messageEn, string messageAr,
        string type = "Info", string? relatedEntityId = null, string? relatedEntityType = null, string? actionUrl = null,
        string? dedupKey = null);
    Task MarkAsReadAsync(string notificationId);
    Task MarkAllAsReadAsync(int userId);
}
