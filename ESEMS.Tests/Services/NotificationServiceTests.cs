using ESEMS.Web.Models;
using ESEMS.Web.Models.Notifications;
using ESEMS.Web.Services.Email;
using ESEMS.Web.Services.Notifications;
using ESEMS.Tests.TestFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace ESEMS.Tests.Services;

public class NotificationServiceTests
{
    [Fact]
    public async Task SendAsync_PersistsNotification_AndPushesRealtime()
    {
        using var db = TestDbContextFactory.Create();
        var realtime = new SpyRealtime();
        var email = new FakeEmail(enabled: false);
        var svc = new NotificationService(db, realtime, email, NullLogger<NotificationService>.Instance);

        await svc.SendAsync(42, "Title", "العنوان", "Body", "النص", "Warning",
            relatedEntityId: "E1", relatedEntityType: "Improvement", actionUrl: "/Improvements/Details/E1");

        var saved = db.Notifications.Single();
        Assert.Equal(42, saved.UserId);
        Assert.Equal("Title", saved.TitleEn);
        Assert.Equal("العنوان", saved.TitleAr);
        Assert.Equal("Warning", saved.Type);
        Assert.False(saved.IsRead);

        Assert.Single(realtime.Pushes);
        Assert.Equal(42, realtime.Pushes[0].userId);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task SendAsync_DispatchesEmail_WhenEnabledAndUserHasAddress()
    {
        using var db = TestDbContextFactory.Create();
        db.CustomUsers.Add(new CustomUser { UserId = 5, Username = "alice", EmailAddress = "alice@example.com", FullName = "Alice" });
        await db.SaveChangesAsync();
        var email = new FakeEmail(enabled: true);
        var svc = new NotificationService(db, new SpyRealtime(), email, NullLogger<NotificationService>.Instance);

        await svc.SendAsync(5, "Approved", "تمت الموافقة", "Your request was approved.", "تمت الموافقة على طلبك.", "Success");

        // Email dispatch is fire-and-forget; give the task a moment to complete.
        await Task.Delay(50);
        Assert.Single(email.Sent);
        Assert.Equal("alice@example.com", email.Sent[0].to);
        Assert.Equal("Approved", email.Sent[0].subject);
        Assert.Contains("Your request was approved.", email.Sent[0].html);
    }

    [Fact]
    public async Task SendAsync_SkipsEmail_WhenEmailServiceDisabled()
    {
        using var db = TestDbContextFactory.Create();
        db.CustomUsers.Add(new CustomUser { UserId = 5, Username = "alice", EmailAddress = "alice@example.com" });
        await db.SaveChangesAsync();
        var email = new FakeEmail(enabled: false);
        var svc = new NotificationService(db, new SpyRealtime(), email, NullLogger<NotificationService>.Instance);

        await svc.SendAsync(5, "Title", "العنوان", "Body", "النص");

        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task SendAsync_DoesNotFail_WhenRealtimePushThrows()
    {
        using var db = TestDbContextFactory.Create();
        var realtime = new ThrowingRealtime();
        var svc = new NotificationService(db, realtime, new FakeEmail(false), NullLogger<NotificationService>.Instance);

        await svc.SendAsync(7, "T", "ت", "M", "ن");

        // In-app notification still persists even if real-time push fails.
        Assert.Single(db.Notifications);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_OrdersByCreatedDesc_AndLimitsTake()
    {
        using var db = TestDbContextFactory.Create();
        for (var i = 0; i < 25; i++)
        {
            db.Notifications.Add(new Notification
            {
                UserId = 1, TitleEn = $"T{i}", TitleAr = $"ت{i}",
                MessageEn = "m", MessageAr = "م",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                Type = "Info",
            });
        }
        db.Notifications.Add(new Notification { UserId = 2, TitleEn = "other user", TitleAr = "م", MessageEn = "m", MessageAr = "م" });
        await db.SaveChangesAsync();

        var svc = new NotificationService(db, new SpyRealtime(), new FakeEmail(false), NullLogger<NotificationService>.Instance);
        var list = await svc.GetUserNotificationsAsync(1, unreadOnly: false, take: 10);

        Assert.Equal(10, list.Count);
        Assert.Equal("T0", list[0].TitleEn);   // newest
        Assert.DoesNotContain(list, n => n.TitleEn == "other user");
    }

    [Fact]
    public async Task GetUserNotificationsAsync_UnreadOnlyFiltersRead()
    {
        using var db = TestDbContextFactory.Create();
        db.Notifications.AddRange(
            new Notification { UserId = 1, TitleEn = "read",   TitleAr = "م", MessageEn = "m", MessageAr = "م", IsRead = true },
            new Notification { UserId = 1, TitleEn = "unread", TitleAr = "م", MessageEn = "m", MessageAr = "م", IsRead = false }
        );
        await db.SaveChangesAsync();
        var svc = new NotificationService(db, new SpyRealtime(), new FakeEmail(false), NullLogger<NotificationService>.Instance);

        var list = await svc.GetUserNotificationsAsync(1, unreadOnly: true);

        Assert.Single(list);
        Assert.Equal("unread", list[0].TitleEn);
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyUnreadForUser()
    {
        using var db = TestDbContextFactory.Create();
        db.Notifications.AddRange(
            new Notification { UserId = 1, TitleEn = "a", TitleAr = "م", MessageEn = "m", MessageAr = "م", IsRead = false },
            new Notification { UserId = 1, TitleEn = "b", TitleAr = "م", MessageEn = "m", MessageAr = "م", IsRead = false },
            new Notification { UserId = 1, TitleEn = "c", TitleAr = "م", MessageEn = "m", MessageAr = "م", IsRead = true  },
            new Notification { UserId = 2, TitleEn = "d", TitleAr = "م", MessageEn = "m", MessageAr = "م", IsRead = false }
        );
        await db.SaveChangesAsync();
        var svc = new NotificationService(db, new SpyRealtime(), new FakeEmail(false), NullLogger<NotificationService>.Instance);

        Assert.Equal(2, await svc.GetUnreadCountAsync(1));
        Assert.Equal(1, await svc.GetUnreadCountAsync(2));
    }

    [Fact]
    public async Task MarkAsReadAsync_SetsReadTimestamp()
    {
        using var db = TestDbContextFactory.Create();
        var n = new Notification { UserId = 1, TitleEn = "a", TitleAr = "م", MessageEn = "m", MessageAr = "م" };
        db.Notifications.Add(n);
        await db.SaveChangesAsync();
        var svc = new NotificationService(db, new SpyRealtime(), new FakeEmail(false), NullLogger<NotificationService>.Instance);

        await svc.MarkAsReadAsync(n.Id);

        var updated = db.Notifications.Single();
        Assert.True(updated.IsRead);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_FlipsOnlyTargetUserUnread()
    {
        using var db = TestDbContextFactory.Create();
        db.Notifications.AddRange(
            new Notification { UserId = 1, TitleEn = "a", TitleAr = "م", MessageEn = "m", MessageAr = "م", IsRead = false },
            new Notification { UserId = 1, TitleEn = "b", TitleAr = "م", MessageEn = "m", MessageAr = "م", IsRead = false },
            new Notification { UserId = 2, TitleEn = "c", TitleAr = "م", MessageEn = "m", MessageAr = "م", IsRead = false }
        );
        await db.SaveChangesAsync();
        var svc = new NotificationService(db, new SpyRealtime(), new FakeEmail(false), NullLogger<NotificationService>.Instance);

        await svc.MarkAllAsReadAsync(1);

        Assert.Equal(2, db.Notifications.Count(n => n.UserId == 1 && n.IsRead));
        Assert.Equal(1, db.Notifications.Count(n => n.UserId == 2 && !n.IsRead));
    }

    private sealed class SpyRealtime : IRealtimeNotifier
    {
        public List<(int userId, string title, string type)> Pushes { get; } = new();
        public Task PushToUserAsync(int userId, string title, string message, string type = "Info", string? actionUrl = null)
        {
            Pushes.Add((userId, title, type));
            return Task.CompletedTask;
        }
        public Task BroadcastDashboardRefreshAsync(string entityType) => Task.CompletedTask;
    }

    private sealed class ThrowingRealtime : IRealtimeNotifier
    {
        public Task PushToUserAsync(int userId, string title, string message, string type = "Info", string? actionUrl = null)
            => throw new InvalidOperationException("SignalR hub not reachable");
        public Task BroadcastDashboardRefreshAsync(string entityType) => Task.CompletedTask;
    }

    private sealed class FakeEmail : IEmailService
    {
        private readonly bool _enabled;
        public FakeEmail(bool enabled) { _enabled = enabled; }
        public bool IsEnabled => _enabled;
        public List<(string to, string subject, string html)> Sent { get; } = new();
        public Task<bool> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add((toAddress, subject, htmlBody));
            return Task.FromResult(true);
        }
        public Task<(bool ok, string message)> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult((true, "ok"));
    }
}
