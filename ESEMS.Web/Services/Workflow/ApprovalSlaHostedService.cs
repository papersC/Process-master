using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Workflow;
using ESEMS.Web.Services.Notifications;

namespace ESEMS.Web.Services.Workflow;

/// <summary>
/// Scans pending workflow steps on a timer and enforces two time-based
/// behaviors:
///
///   1. <b>Auto-escalation</b> — when a step's <see cref="WorkflowStep.DueAt"/>
///      has passed and the rule has an <c>EscalationUserId</c>, swap the
///      approver to the escalation user and notify them.
///
///   2. <b>Delegation return</b> — when a step has <c>DelegationExpiresAt</c>
///      in the past, revert the approver to <c>DelegatedFromUserId</c> so
///      temporary delegations (e.g. covering someone's vacation) bounce
///      back automatically.
///
/// Both actions append an audit line to <c>Comments</c> and update the
/// parent <c>WorkflowInstance.ApproverUserId</c> so PendingApprovals
/// queries keep working.
/// </summary>
public class ApprovalSlaHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<ApprovalSlaHostedService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

    public ApprovalSlaHostedService(IServiceProvider sp, ILogger<ApprovalSlaHostedService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Approval SLA worker starting; tick every {Minutes} minutes", _interval.TotalMinutes);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TickAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Approval SLA worker tick failed");
            }
            try { await Task.Delay(_interval, ct); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notify = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await ProcessTickAsync(db, notify, DateTime.UtcNow, _logger, ct);
    }

    // Pure-logic entry point — takes pre-resolved dependencies so tests can
    // call it with an in-memory DbContext + fake notifier without mocking
    // IServiceProvider.
    internal static async Task ProcessTickAsync(
        ApplicationDbContext db,
        INotificationService notify,
        DateTime now,
        ILogger logger,
        CancellationToken ct)
    {
        // 1) Revert expired delegations first so the escalation step sees
        //    the correct ApproverUserId.
        var expiredDelegations = await db.WorkflowSteps
            .Include(s => s.WorkflowInstance)
            .Where(s => s.Action == "Pending"
                     && s.DelegationExpiresAt != null
                     && s.DelegationExpiresAt <= now
                     && s.DelegatedFromUserId != null)
            .ToListAsync(ct);

        foreach (var step in expiredDelegations)
        {
            var originalUserId = step.DelegatedFromUserId;
            var originalName = step.DelegatedFromName;
            var currentName = step.ApproverName ?? "(unassigned)";
            step.ApproverUserId = originalUserId;
            step.ApproverName = originalName;
            step.DelegatedFromUserId = null;
            step.DelegatedFromName = null;
            step.DelegationExpiresAt = null;

            var trail = $"[Delegation expired {now:yyyy-MM-dd HH:mm} UTC] reverted from {currentName} to {originalName}";
            step.Comments = string.IsNullOrWhiteSpace(step.Comments) ? trail : $"{step.Comments}\n{trail}";

            if (step.WorkflowInstance != null && step.StepLevel == step.WorkflowInstance.CurrentLevel)
                step.WorkflowInstance.ApproverUserId = originalUserId;

            if (originalUserId.HasValue)
            {
                await notify.SendAsync(originalUserId.Value,
                    "Delegated approval returned", "تم إرجاع الاعتماد المفوَّض",
                    $"A delegation on workflow {step.WorkflowInstanceId} has expired and the approval is back with you.",
                    $"انتهى تفويض اعتماد على سير العمل {step.WorkflowInstanceId} وعاد إليك.",
                    "Info", step.WorkflowInstance?.EntityId, step.WorkflowInstance?.EntityType, "/Workflow/PendingApprovals");
            }
            logger.LogInformation("Reverted expired delegation on step {StepId}; approver back to {UserId}", step.Id, originalUserId);
        }

        // 2) Auto-escalate steps past SLA with an escalation target configured.
        var overdueSteps = await db.WorkflowSteps
            .Include(s => s.WorkflowInstance)
            .Where(s => s.Action == "Pending"
                     && s.DueAt != null
                     && s.DueAt <= now
                     && s.EscalatedAt == null)
            .ToListAsync(ct);

        foreach (var step in overdueSteps)
        {
            if (step.WorkflowInstance == null) continue;

            // Look up the rule that owns this workflow so we know who to
            // escalate to. Fallback to the first active rule for the entity
            // type if the original rule was deleted.
            var configId = step.WorkflowInstance.ApprovalConfigurationId;
            ApprovalConfiguration? rule = null;
            if (!string.IsNullOrWhiteSpace(configId))
                rule = await db.ApprovalConfigurations.FirstOrDefaultAsync(c => c.Id == configId, ct);
            rule ??= await db.ApprovalConfigurations
                .FirstOrDefaultAsync(c => c.EntityType == step.WorkflowInstance.EntityType && c.IsActive, ct);

            if (rule?.EscalationUserId == null)
            {
                // No escalation configured — mark as escalated-with-no-target so
                // we don't re-trigger; just log a warning.
                step.EscalatedAt = now;
                var trail = $"[SLA reached {now:yyyy-MM-dd HH:mm} UTC] no escalation user configured";
                step.Comments = string.IsNullOrWhiteSpace(step.Comments) ? trail : $"{step.Comments}\n{trail}";
                logger.LogWarning("Step {StepId} past SLA but no escalation user configured", step.Id);
                continue;
            }

            var originalName = step.ApproverName ?? "(unassigned)";
            step.ApproverUserId = rule.EscalationUserId;
            step.ApproverName = rule.EscalationUserName ?? $"User {rule.EscalationUserId}";
            step.EscalatedAt = now;

            var escalateTrail = $"[Auto-escalated {now:yyyy-MM-dd HH:mm} UTC] SLA elapsed, from {originalName} to {step.ApproverName}";
            step.Comments = string.IsNullOrWhiteSpace(step.Comments) ? escalateTrail : $"{step.Comments}\n{escalateTrail}";

            if (step.StepLevel == step.WorkflowInstance.CurrentLevel)
                step.WorkflowInstance.ApproverUserId = rule.EscalationUserId;

            var arType = ArEntityType(step.WorkflowInstance.EntityType);
            await notify.SendAsync(rule.EscalationUserId.Value,
                "Escalated approval pending", "اعتماد مُصعَّد بانتظار الإجراء",
                $"A {step.WorkflowInstance.EntityType} was escalated to you after its SLA elapsed.",
                $"تم تصعيد {arType} إليك بعد انتهاء مدة الاعتماد.",
                "Warning", step.WorkflowInstance.EntityId, step.WorkflowInstance.EntityType, "/Workflow/PendingApprovals");

            logger.LogInformation("Escalated step {StepId} to user {UserId}", step.Id, rule.EscalationUserId);
        }

        if (expiredDelegations.Count > 0 || overdueSteps.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Background service has no current culture, so map the EntityType
    /// code (e.g. "Improvement") directly to the Arabic noun used in
    /// notification bodies. Falls back to the raw code if unknown.
    /// </summary>
    private static string ArEntityType(string? entityType) => (entityType ?? string.Empty) switch
    {
        "Improvement" => "تحسين",
        "ChangeRequest" => "طلب تغيير",
        "Process" => "عملية",
        "ProcessGroup" => "مجموعة عمليات",
        "ProcessTask" => "إجراء",
        "Activity" => "نشاط",
        "Service" => "خدمة",
        "Asset" => "أصل",
        "EnterpriseRisk" => "مخاطر",
        "OrganizationUnit" => "وحدة تنظيمية",
        "StrategicObjective" => "هدف استراتيجي",
        "Incident" => "حادثة",
        "Problem" => "مشكلة",
        "Category" => "فئة",
        "CustomUser" => "مستخدم",
        _ => entityType ?? string.Empty
    };
}
