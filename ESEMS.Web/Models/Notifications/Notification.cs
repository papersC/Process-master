namespace ESEMS.Web.Models.Notifications;

public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int? UserId { get; set; }
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string MessageEn { get; set; } = string.Empty;
    public string MessageAr { get; set; } = string.Empty;
    public string Type { get; set; } = "Info"; // Info, Warning, Error, Success
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Stable de-dup key for repeating notifications (background sweeps,
    /// scheduled reminders). Title-based dedup proved fragile because the
    /// stall-detection title bakes the day count in ("9 initiatives idle
    /// 91+ days"), so a tick from 90→91 days slipped past dedup. Callers pass
    /// a deterministic key derived from (recipient, severity band, role,
    /// digest type) and SendAsync suppresses any unread notification with
    /// the same key inside a 24h window.</summary>
    public string? DedupKey { get; set; }
}
