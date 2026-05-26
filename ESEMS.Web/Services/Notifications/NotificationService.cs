using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Notifications;
using ESEMS.Web.Services.Email;

namespace ESEMS.Web.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IRealtimeNotifier _notifier;
    private readonly IEmailService _email;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDbContext context, IRealtimeNotifier notifier, IEmailService email, ILogger<NotificationService> logger)
    {
        _context = context;
        _notifier = notifier;
        _email = email;
        _logger = logger;
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false, int take = 20)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .AsQueryable();

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query.Take(take).ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task SendAsync(int userId, string titleEn, string titleAr, string messageEn, string messageAr,
        string type = "Info", string? relatedEntityId = null, string? relatedEntityType = null, string? actionUrl = null,
        string? dedupKey = null)
    {
        // De-dup guard. Background sweeps re-emit the same digest every run;
        // without this the bell accumulates duplicates as the job ticks.
        // Prefer a stable dedupKey when the caller supplies one — title-only
        // dedup proved fragile because the stall-detection title bakes the
        // day count in ("9 initiatives idle 91+ days"), so a tick from 90→91
        // silently bypassed the guard. When no dedupKey is given, fall back
        // to the older title + entity-type heuristic for ad-hoc senders.
        // Window: 24h — once the user has read the previous copy, a fresh
        // notification can land the next day.
        if (!string.IsNullOrEmpty(dedupKey))
        {
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var duplicateExists = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.UserId == userId
                            && !n.IsRead
                            && n.DedupKey == dedupKey
                            && n.CreatedAt >= cutoff);
            if (duplicateExists)
            {
                _logger.LogDebug(
                    "Suppressed duplicate notification (key={DedupKey}) for user {UserId}",
                    dedupKey, userId);
                return;
            }
        }
        else if (!string.IsNullOrEmpty(relatedEntityType))
        {
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var duplicateExists = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.UserId == userId
                            && !n.IsRead
                            && n.TitleEn == titleEn
                            && n.RelatedEntityType == relatedEntityType
                            && n.CreatedAt >= cutoff);
            if (duplicateExists)
            {
                _logger.LogDebug(
                    "Suppressed duplicate notification (title-match) for user {UserId} ({EntityType}: {Title})",
                    userId, relatedEntityType, titleEn);
                return;
            }
        }

        var notification = new Notification
        {
            UserId = userId,
            TitleEn = titleEn,
            TitleAr = titleAr,
            MessageEn = messageEn,
            MessageAr = messageAr,
            Type = type,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            ActionUrl = actionUrl,
            DedupKey = dedupKey
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Push real-time via SignalR
        try
        {
            await _notifier.PushToUserAsync(userId, titleEn, messageEn, type, actionUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push real-time notification to user {UserId}", userId);
        }

        // Email delivery — best-effort. When email is disabled or SMTP
        // fails, the in-app notification above still reaches the user.
        if (_email.IsEnabled)
        {
            try
            {
                var user = await _context.CustomUsers
                    .Where(u => u.UserId == userId)
                    .Select(u => new { u.EmailAddress, u.FullName })
                    .FirstOrDefaultAsync();
                if (user != null && !string.IsNullOrWhiteSpace(user.EmailAddress))
                {
                    var absoluteUrl = !string.IsNullOrWhiteSpace(actionUrl) ? actionUrl : "";
                    var html = BuildEmailBody(titleEn, messageEn, absoluteUrl);
                    _ = _email.SendAsync(user.EmailAddress, titleEn, html);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email dispatch failed for notification to user {UserId}", userId);
            }
        }
    }

    private static string BuildEmailBody(string title, string message, string? actionUrl)
    {
        var html = new System.Text.StringBuilder();
        html.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#111827;max-width:560px;\">");
        html.Append($"<h2 style=\"color:#005B99;margin-bottom:8px;\">{System.Net.WebUtility.HtmlEncode(title)}</h2>");
        html.Append($"<p style=\"font-size:15px;line-height:1.5;\">{System.Net.WebUtility.HtmlEncode(message)}</p>");
        if (!string.IsNullOrWhiteSpace(actionUrl))
        {
            html.Append($"<p><a href=\"{System.Net.WebUtility.HtmlEncode(actionUrl)}\" style=\"display:inline-block;padding:10px 20px;background:#005B99;color:white;text-decoration:none;border-radius:6px;font-weight:600;\">Open in ESEMS</a></p>");
        }
        html.Append("<hr style=\"border:none;border-top:1px solid #e5e7eb;margin:24px 0;\"/>");
        html.Append("<p style=\"font-size:12px;color:#6b7280;\">This is an automated message from ESEMS. Please do not reply.</p>");
        html.Append("</div>");
        return html.ToString();
    }

    public async Task MarkAsReadAsync(string notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
