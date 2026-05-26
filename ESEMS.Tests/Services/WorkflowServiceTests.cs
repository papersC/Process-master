using ESEMS.Web;
using ESEMS.Web.Models.Workflow;
using ESEMS.Web.Models.Notifications;
using ESEMS.Web.Services.Notifications;
using ESEMS.Web.Services.Workflow;
using ESEMS.Tests.TestFixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ESEMS.Tests.Services;

public class WorkflowServiceTests
{
    [Fact]
    public async Task CreateAsync_PicksRuleMatchingContext_OverCatchAll()
    {
        using var db = TestDbContextFactory.Create();
        db.ApprovalConfigurations.AddRange(
            new ApprovalConfiguration { Id = "CATCH", EntityType = "Improvement", IsActive = true, Priority = 200, Level1ApproverUserId = 1, Level1ApproverName = "Catch-All Approver" },
            new ApprovalConfiguration { Id = "HIGH",  EntityType = "Improvement", IsActive = true, Priority = 100, MinCostSavings = 1000m, Level1ApproverUserId = 2, Level1ApproverName = "High-Value Approver" }
        );
        await db.SaveChangesAsync();

        var svc = NewService(db, out var notify);
        var wf = await svc.CreateAsync("E1", "Improvement", 99, "Submitter", context: new ApprovalContext { CostSavings = 5000m });

        Assert.Equal("HIGH", wf.ApprovalConfigurationId);
        Assert.Equal(2, wf.ApproverUserId);
        Assert.Single(notify.Sent);
        Assert.Equal(2, notify.Sent[0].userId);
    }

    [Fact]
    public async Task CreateAsync_FallsBackToCatchAll_WhenNoRuleMatches()
    {
        using var db = TestDbContextFactory.Create();
        db.ApprovalConfigurations.AddRange(
            new ApprovalConfiguration { Id = "CATCH", EntityType = "Improvement", IsActive = true, Priority = 200, Level1ApproverUserId = 1, Level1ApproverName = "Catch-All" },
            new ApprovalConfiguration { Id = "HIGH",  EntityType = "Improvement", IsActive = true, Priority = 100, MinCostSavings = 10000m, Level1ApproverUserId = 2 }
        );
        await db.SaveChangesAsync();

        var svc = NewService(db, out _);
        var wf = await svc.CreateAsync("E2", "Improvement", 99, "Submitter", context: new ApprovalContext { CostSavings = 50m });

        Assert.Equal("CATCH", wf.ApprovalConfigurationId);
    }

    [Fact]
    public async Task CreateAsync_SetsStepDueAt_WhenRuleHasSlaHours()
    {
        using var db = TestDbContextFactory.Create();
        db.ApprovalConfigurations.Add(new ApprovalConfiguration
        {
            Id = "SLA", EntityType = "Improvement", IsActive = true, Priority = 100,
            Level1ApproverUserId = 1, Level1SlaHours = 24
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, out _);
        var before = DateTime.UtcNow;
        var wf = await svc.CreateAsync("E3", "Improvement", 99, "Submitter");
        var after = DateTime.UtcNow;

        var step = db.WorkflowSteps.Single(s => s.WorkflowInstanceId == wf.Id);
        Assert.NotNull(step.DueAt);
        Assert.InRange(step.DueAt!.Value, before.AddHours(24).AddSeconds(-5), after.AddHours(24).AddSeconds(5));
    }

    [Fact]
    public async Task ProcessActionAsync_L1Approval_AdvancesToL2_AndNotifies()
    {
        using var db = TestDbContextFactory.Create();
        db.ApprovalConfigurations.Add(new ApprovalConfiguration
        {
            Id = "R", EntityType = "Improvement", IsActive = true, Priority = 100,
            Level1ApproverUserId = 1, Level1ApproverName = "L1",
            Level2Required = true, Level2ApproverUserId = 2, Level2ApproverName = "L2"
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, out var notify);
        var wf = await svc.CreateAsync("E4", "Improvement", 99, "Submitter");
        notify.Sent.Clear();

        await svc.ProcessActionAsync(wf.Id, approverUserId: 1, approverName: "L1", action: "Approved", comments: "LGTM");

        var updated = await db.WorkflowInstances.FirstAsync(w => w.Id == wf.Id);
        Assert.Equal(2, updated.CurrentLevel);
        Assert.Equal(WorkflowStatus.UnderReview, updated.Status);
        Assert.Equal(2, updated.ApproverUserId);
        Assert.Equal(2, db.WorkflowSteps.Count(s => s.WorkflowInstanceId == wf.Id));
        Assert.Contains(notify.Sent, n => n.userId == 2);
    }

    [Fact]
    public async Task ProcessActionAsync_FinalApproval_SetsApproved_AndNotifiesSubmitter()
    {
        using var db = TestDbContextFactory.Create();
        db.ApprovalConfigurations.Add(new ApprovalConfiguration
        {
            Id = "R", EntityType = "Improvement", IsActive = true, Priority = 100,
            Level1ApproverUserId = 1, Level1ApproverName = "Sole Approver"
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, out var notify);
        var wf = await svc.CreateAsync("E5", "Improvement", 99, "Submitter");
        notify.Sent.Clear();

        await svc.ProcessActionAsync(wf.Id, 1, "Sole Approver", "Approved");

        var updated = await db.WorkflowInstances.FirstAsync(w => w.Id == wf.Id);
        Assert.Equal(WorkflowStatus.Approved, updated.Status);
        Assert.Contains(notify.Sent, n => n.userId == 99 && n.type == "Success");
    }

    [Fact]
    public async Task ProcessActionAsync_Rejected_SetsRejected_AndNotifiesSubmitter()
    {
        using var db = TestDbContextFactory.Create();
        db.ApprovalConfigurations.Add(new ApprovalConfiguration
        {
            Id = "R", EntityType = "Improvement", IsActive = true, Priority = 100,
            Level1ApproverUserId = 1,
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, out var notify);
        var wf = await svc.CreateAsync("E6", "Improvement", 99, "Submitter");
        notify.Sent.Clear();

        await svc.ProcessActionAsync(wf.Id, 1, "Approver", "Rejected", comments: "Not justified.");

        var updated = await db.WorkflowInstances.FirstAsync(w => w.Id == wf.Id);
        Assert.Equal(WorkflowStatus.Rejected, updated.Status);
        Assert.Contains(notify.Sent, n => n.userId == 99 && n.type == "Error");
    }

    [Fact]
    public async Task ProcessActionAsync_RejectsUnauthorizedCaller()
    {
        using var db = TestDbContextFactory.Create();
        db.ApprovalConfigurations.Add(new ApprovalConfiguration
        {
            Id = "R", EntityType = "Improvement", IsActive = true, Priority = 100,
            Level1ApproverUserId = 1,
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, out _);
        var wf = await svc.CreateAsync("E7", "Improvement", 99, "Submitter");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.ProcessActionAsync(wf.Id, approverUserId: 42, "Imposter", "Approved"));
    }

    private static WorkflowService NewService(ESEMS.Web.Data.ApplicationDbContext db, out FakeNotifier notify)
    {
        notify = new FakeNotifier();
        // Build a real ResourceManagerStringLocalizer pointed at the
        // SharedResource resx so notification templates can resolve
        // EntityType_<code> keys (Workflow now uses these for AR copy).
        var rmFactory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "" }),
            NullLoggerFactory.Instance);
        IStringLocalizer<SharedResource> localizer = new StringLocalizer<SharedResource>(rmFactory);
        return new WorkflowService(db, notify, NullLogger<WorkflowService>.Instance, localizer);
    }

    private sealed class FakeNotifier : INotificationService
    {
        public List<(int userId, string titleEn, string messageEn, string type)> Sent { get; } = new();

        public Task SendAsync(int userId, string titleEn, string titleAr, string messageEn, string messageAr,
            string type = "Info", string? relatedEntityId = null, string? relatedEntityType = null, string? actionUrl = null,
            string? dedupKey = null)
        {
            Sent.Add((userId, titleEn, messageEn, type));
            return Task.CompletedTask;
        }

        public Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false, int take = 20) => Task.FromResult(new List<Notification>());
        public Task<int> GetUnreadCountAsync(int userId) => Task.FromResult(0);
        public Task MarkAsReadAsync(string notificationId) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(int userId) => Task.CompletedTask;
    }
}
