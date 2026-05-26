using ESEMS.Web.Models.Workflow;
using ESEMS.Web.Models.Notifications;
using ESEMS.Web.Services.Notifications;
using ESEMS.Web.Services.Workflow;
using ESEMS.Tests.TestFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace ESEMS.Tests.Services;

public class ApprovalSlaHostedServiceTests
{
    [Fact]
    public async Task ProcessTickAsync_RevertsExpiredDelegation_ToOriginalApprover()
    {
        using var db = TestDbContextFactory.Create();
        var instance = new WorkflowInstance { Id = "WI1", EntityId = "E1", EntityType = "Improvement", CurrentLevel = 1 };
        db.WorkflowInstances.Add(instance);
        db.WorkflowSteps.Add(new WorkflowStep
        {
            Id = "S1",
            WorkflowInstanceId = instance.Id,
            StepLevel = 1,
            Action = "Pending",
            ApproverUserId = 99,
            ApproverName = "Covering User",
            DelegatedFromUserId = 42,
            DelegatedFromName = "Original User",
            DelegationExpiresAt = DateTime.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();

        var notify = new FakeNotifier();
        await ApprovalSlaHostedService.ProcessTickAsync(db, notify, DateTime.UtcNow, NullLogger.Instance, default);

        var step = db.WorkflowSteps.Single();
        Assert.Equal(42, step.ApproverUserId);
        Assert.Equal("Original User", step.ApproverName);
        Assert.Null(step.DelegatedFromUserId);
        Assert.Null(step.DelegationExpiresAt);
        Assert.Contains("Delegation expired", step.Comments);
        Assert.Equal(42, db.WorkflowInstances.Single().ApproverUserId);
        Assert.Single(notify.Sent);
        Assert.Equal(42, notify.Sent[0].userId);
    }

    [Fact]
    public async Task ProcessTickAsync_EscalatesOverdueStep_WhenRuleHasEscalationUser()
    {
        using var db = TestDbContextFactory.Create();
        var rule = new ApprovalConfiguration
        {
            Id = "R1",
            EntityType = "Improvement",
            IsActive = true,
            EscalationUserId = 7,
            EscalationUserName = "Escalation Target",
        };
        db.ApprovalConfigurations.Add(rule);
        var instance = new WorkflowInstance
        {
            Id = "WI2",
            EntityId = "E2",
            EntityType = "Improvement",
            CurrentLevel = 1,
            ApprovalConfigurationId = rule.Id,
        };
        db.WorkflowInstances.Add(instance);
        db.WorkflowSteps.Add(new WorkflowStep
        {
            Id = "S2",
            WorkflowInstanceId = instance.Id,
            StepLevel = 1,
            Action = "Pending",
            ApproverUserId = 3,
            ApproverName = "Sleepy Approver",
            DueAt = DateTime.UtcNow.AddHours(-2),
        });
        await db.SaveChangesAsync();

        var notify = new FakeNotifier();
        await ApprovalSlaHostedService.ProcessTickAsync(db, notify, DateTime.UtcNow, NullLogger.Instance, default);

        var step = db.WorkflowSteps.Single();
        Assert.Equal(7, step.ApproverUserId);
        Assert.Equal("Escalation Target", step.ApproverName);
        Assert.NotNull(step.EscalatedAt);
        Assert.Contains("Auto-escalated", step.Comments);
        Assert.Equal(7, db.WorkflowInstances.Single().ApproverUserId);
        Assert.Single(notify.Sent);
    }

    [Fact]
    public async Task ProcessTickAsync_MarksEscalatedEvenWithNoTarget_SoNoRepeatTrigger()
    {
        using var db = TestDbContextFactory.Create();
        var instance = new WorkflowInstance { Id = "WI3", EntityId = "E3", EntityType = "Improvement", CurrentLevel = 1 };
        db.WorkflowInstances.Add(instance);
        db.WorkflowSteps.Add(new WorkflowStep
        {
            Id = "S3",
            WorkflowInstanceId = instance.Id,
            StepLevel = 1,
            Action = "Pending",
            ApproverUserId = 3,
            ApproverName = "Sleepy Approver",
            DueAt = DateTime.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();

        var notify = new FakeNotifier();
        await ApprovalSlaHostedService.ProcessTickAsync(db, notify, DateTime.UtcNow, NullLogger.Instance, default);

        var step = db.WorkflowSteps.Single();
        Assert.Equal(3, step.ApproverUserId);
        Assert.NotNull(step.EscalatedAt);
        Assert.Contains("no escalation user configured", step.Comments);
        Assert.Empty(notify.Sent);
    }

    [Fact]
    public async Task ProcessTickAsync_IgnoresStep_WhenDueAtInFuture()
    {
        using var db = TestDbContextFactory.Create();
        var instance = new WorkflowInstance { Id = "WI4", EntityId = "E4", EntityType = "Improvement", CurrentLevel = 1 };
        db.WorkflowInstances.Add(instance);
        db.WorkflowSteps.Add(new WorkflowStep
        {
            Id = "S4",
            WorkflowInstanceId = instance.Id,
            StepLevel = 1,
            Action = "Pending",
            ApproverUserId = 3,
            DueAt = DateTime.UtcNow.AddHours(+2),
        });
        await db.SaveChangesAsync();

        var notify = new FakeNotifier();
        await ApprovalSlaHostedService.ProcessTickAsync(db, notify, DateTime.UtcNow, NullLogger.Instance, default);

        var step = db.WorkflowSteps.Single();
        Assert.Null(step.EscalatedAt);
        Assert.Equal(3, step.ApproverUserId);
        Assert.Empty(notify.Sent);
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
