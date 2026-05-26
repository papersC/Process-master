using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Workflow;
using ESEMS.Web.Services.Notifications;

namespace ESEMS.Web.Services.Workflow;

public class WorkflowService : IWorkflowService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<WorkflowService> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public WorkflowService(ApplicationDbContext context, INotificationService notificationService, ILogger<WorkflowService> logger, IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Maps a raw EntityType code (e.g. "Improvement", "ChangeRequest") to
    /// the localized noun used in user-facing notifications. Falls back to
    /// the code itself if no resource key is present so we never emit empty
    /// notification bodies.
    /// </summary>
    private string LocalizedEntityType(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType)) return entityType;
        var s = _localizer[$"EntityType_{entityType}"];
        return s.ResourceNotFound ? entityType : s.Value;
    }

    /// <summary>
    /// Same as LocalizedEntityType but explicitly returns the Arabic noun
    /// regardless of current culture — used when building bilingual messages
    /// where the English text comes from `entityType` directly.
    /// </summary>
    private string ArabicEntityType(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType)) return entityType;
        // Look up via resource fallback: temporarily switch culture so the
        // ar.resx value is returned no matter what culture the caller is in.
        var prev = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ar-AE");
            return LocalizedEntityType(entityType);
        }
        finally { System.Globalization.CultureInfo.CurrentUICulture = prev; }
    }

    public async Task<WorkflowInstance?> GetByIdAsync(string id)
    {
        return await _context.WorkflowInstances
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<List<WorkflowInstance>> GetPendingApprovalsAsync(int approverUserId)
    {
        return await _context.WorkflowInstances
            .Include(w => w.Steps)
            .Where(w => w.ApproverUserId == approverUserId &&
                        (w.Status == WorkflowStatus.Submitted || w.Status == WorkflowStatus.UnderReview))
            .OrderByDescending(w => w.SubmittedAt)
            .ToListAsync();
    }

    public async Task<WorkflowInstance> CreateAsync(string entityId, string entityType, int submitterId, string submitterName, string? notes = null, ApprovalContext? context = null)
    {
        // Load every active rule for this entity type in priority order.
        // Rules with a lower Priority value are evaluated first. The first
        // rule whose condition bands match the submission context wins.
        // If no rule matches, we fall back to the first rule with no
        // conditions (a catch-all), or to a one-level "no approver" rule
        // if the admin has not configured anything yet.
        var rules = await _context.ApprovalConfigurations
            .Where(c => c.EntityType == entityType && c.IsActive)
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync();

        var ctx = context ?? new ApprovalContext();
        var config = rules.FirstOrDefault(r => r.Matches(ctx));
        if (config == null)
        {
            // No conditional rule matched — fall back to the first rule that
            // has no conditions at all (every band is null). That's the
            // unconditional catch-all.
            config = rules.FirstOrDefault(r =>
                r.MinCostSavings == null && r.MaxCostSavings == null &&
                r.MinImpactScore == null && r.MaxImpactScore == null &&
                r.MinDurationDays == null && r.MaxDurationDays == null &&
                string.IsNullOrWhiteSpace(r.Horizon) &&
                string.IsNullOrWhiteSpace(r.InnovationType));
        }

        var maxLevel = 1;
        int? level1Approver = null;

        if (config != null)
        {
            maxLevel = config.Level2Required ? 2 : 1;
            level1Approver = config.Level1ApproverUserId;
        }

        var workflow = new WorkflowInstance
        {
            EntityId = entityId,
            EntityType = entityType,
            Type = entityType,
            Status = WorkflowStatus.Submitted,
            SubmittedById = submitterId,
            SubmitterName = submitterName,
            SubmittedAt = DateTime.UtcNow,
            CurrentLevel = 1,
            MaxLevel = maxLevel,
            ApproverUserId = level1Approver,
            ApprovalConfigurationId = config?.Id,
            Notes = notes
        };

        _context.WorkflowInstances.Add(workflow);

        _context.WorkflowSteps.Add(new WorkflowStep
        {
            WorkflowInstanceId = workflow.Id,
            StepLevel = 1,
            ApproverUserId = level1Approver,
            ApproverName = config?.Level1ApproverName,
            Action = "Pending",
            DueAt = config?.Level1SlaHours.HasValue == true
                ? DateTime.UtcNow.AddHours(config.Level1SlaHours!.Value)
                : null
        });

        await _context.SaveChangesAsync();

        if (level1Approver.HasValue)
        {
            var arType = ArabicEntityType(entityType);
            await _notificationService.SendAsync(level1Approver.Value,
                $"Approval Required: {entityType}", $"موافقة مطلوبة: {arType}",
                $"A new {entityType} submitted by {submitterName} requires your approval.",
                $"تم تقديم {arType} جديد من قبل {submitterName} ويتطلب موافقتك.",
                "Warning", entityId, entityType, "/Workflow/PendingApprovals");
        }

        return workflow;
    }

    public async Task ProcessActionAsync(string workflowId, int approverUserId, string approverName, string action, string? comments = null)
    {
        var workflow = await _context.WorkflowInstances
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId)
            ?? throw new KeyNotFoundException($"Workflow {workflowId} not found.");

        // F-004: refuse actions on a workflow that already reached a terminal
        // decision. Without this, the assigned approver could re-POST and flip a
        // Rejected/Returned/Cancelled item to Approved, or fire duplicate
        // notifications, since the status is reassigned in the switch below.
        if (workflow.Status is not (WorkflowStatus.Submitted or WorkflowStatus.UnderReview))
            throw new InvalidOperationException(
                $"This request is already {workflow.Status} and can no longer be actioned.");

        if (workflow.ApproverUserId != approverUserId)
            throw new UnauthorizedAccessException("You are not the assigned approver.");

        // FLOW-001 (self-approval): a submitter must never approve their own
        // submission. This is the single chokepoint every approval chain
        // (Improvements, ChangeRequest, future entity types) flows through,
        // so guarding here covers all callers. Reject/Return are governance-
        // neutral (returning your own item for correction is harmless), so we
        // only block the "Approved" action.
        if (action == "Approved"
            && workflow.SubmittedById.HasValue
            && workflow.SubmittedById.Value == approverUserId)
        {
            throw new InvalidOperationException(
                "You cannot approve a request that you submitted. Approval must be performed by a different user.");
        }

        var currentStep = workflow.Steps.FirstOrDefault(s => s.StepLevel == workflow.CurrentLevel && s.Action == "Pending");
        if (currentStep != null)
        {
            currentStep.ApproverUserId = approverUserId;
            currentStep.ApproverName = approverName;
            currentStep.Action = action;
            currentStep.Comments = comments;
            currentStep.ActionDate = DateTime.UtcNow;
        }

        workflow.UpdatedAt = DateTime.UtcNow;

        // FLOW-008 (notify before commit): notifications used to be awaited
        // INSIDE this switch, before the SaveChangesAsync below. If the save
        // then failed (concurrency, FK, etc.) the approver/submitter had
        // already received a "your request was approved" alert for a state
        // change that never persisted. We now QUEUE the notifications and
        // dispatch them only after the commit succeeds.
        var pendingNotifications = new List<Func<Task>>();

        switch (action)
        {
            case "Approved":
                if (workflow.CurrentLevel < workflow.MaxLevel)
                {
                    workflow.CurrentLevel++;
                    workflow.Status = WorkflowStatus.UnderReview;
                    // Walk to the SAME rule's Level 2 approver. Falls back
                    // to the first active rule if the stamped rule has been
                    // deleted since submission.
                    var config = !string.IsNullOrWhiteSpace(workflow.ApprovalConfigurationId)
                        ? await _context.ApprovalConfigurations.FirstOrDefaultAsync(c => c.Id == workflow.ApprovalConfigurationId)
                        : null;
                    config ??= await _context.ApprovalConfigurations
                        .FirstOrDefaultAsync(c => c.EntityType == workflow.EntityType && c.IsActive);

                    // FLOW-003 (dual-level one-person clearance): one human must
                    // not be able to satisfy BOTH approval levels. Block when the
                    // configured Level 2 approver is the same person who just
                    // acted at Level 1 (== the current approver, since we already
                    // verified approverUserId == workflow.ApproverUserId above).
                    // Without this, an admin who is both Level1 and Level2 on a
                    // rule could clear a two-level workflow single-handedly,
                    // defeating the segregation-of-duties the second level exists
                    // to enforce.
                    if (config?.Level2ApproverUserId != null
                        && config.Level2ApproverUserId.Value == approverUserId)
                    {
                        throw new InvalidOperationException(
                            "You are configured as both the Level 1 and Level 2 approver for this request. " +
                            "A second, different approver is required to complete dual-level approval.");
                    }

                    workflow.ApproverUserId = config?.Level2ApproverUserId;

                    _context.WorkflowSteps.Add(new WorkflowStep
                    {
                        WorkflowInstanceId = workflow.Id,
                        StepLevel = workflow.CurrentLevel,
                        ApproverUserId = config?.Level2ApproverUserId,
                        ApproverName = config?.Level2ApproverName,
                        Action = "Pending",
                        DueAt = config?.Level2SlaHours.HasValue == true
                            ? DateTime.UtcNow.AddHours(config.Level2SlaHours!.Value)
                            : null
                    });

                    if (config?.Level2ApproverUserId != null)
                    {
                        var level2ApproverId = config.Level2ApproverUserId.Value;
                        var arType2 = ArabicEntityType(workflow.EntityType);
                        pendingNotifications.Add(() => _notificationService.SendAsync(level2ApproverId,
                            "Level 2 Approval Required", "موافقة المستوى الثاني مطلوبة",
                            $"A {workflow.EntityType} requires your Level 2 approval.",
                            $"يتطلب {arType2} موافقتك من المستوى الثاني.",
                            "Warning", workflow.EntityId, workflow.EntityType, "/Workflow/PendingApprovals"));
                    }
                }
                else
                {
                    workflow.Status = WorkflowStatus.Approved;
                    if (workflow.SubmittedById.HasValue)
                    {
                        var submitterId = workflow.SubmittedById.Value;
                        var arType3 = ArabicEntityType(workflow.EntityType);
                        pendingNotifications.Add(() => _notificationService.SendAsync(submitterId,
                            $"{workflow.EntityType} Approved", $"تمت الموافقة على {arType3}",
                            $"Your {workflow.EntityType} has been approved.", $"تمت الموافقة على {arType3} الخاص بك.",
                            "Success", workflow.EntityId, workflow.EntityType));
                    }
                }
                break;

            case "Rejected":
                workflow.Status = WorkflowStatus.Rejected;
                if (workflow.SubmittedById.HasValue)
                {
                    var submitterId = workflow.SubmittedById.Value;
                    var arType4 = ArabicEntityType(workflow.EntityType);
                    pendingNotifications.Add(() => _notificationService.SendAsync(submitterId,
                        $"{workflow.EntityType} Rejected", $"تم رفض {arType4}",
                        $"Your {workflow.EntityType} was rejected. Reason: {comments ?? "N/A"}",
                        $"تم رفض {arType4} الخاص بك. السبب: {comments ?? "غير محدد"}",
                        "Error", workflow.EntityId, workflow.EntityType));
                }
                break;

            case "ReturnedForCorrection":
                workflow.Status = WorkflowStatus.ReturnedForCorrection;
                if (workflow.SubmittedById.HasValue)
                {
                    var submitterId = workflow.SubmittedById.Value;
                    var arType5 = ArabicEntityType(workflow.EntityType);
                    pendingNotifications.Add(() => _notificationService.SendAsync(submitterId,
                        $"{workflow.EntityType} Returned", $"تمت إعادة {arType5}",
                        $"Your {workflow.EntityType} was returned for correction: {comments ?? ""}",
                        $"تمت إعادة {arType5} الخاص بك للتصحيح: {comments ?? ""}",
                        "Warning", workflow.EntityId, workflow.EntityType));
                }
                break;
        }

        await _context.SaveChangesAsync();

        // FLOW-008: state change is now durably committed — safe to notify.
        foreach (var send in pendingNotifications)
        {
            await send();
        }
    }

    public async Task<List<WorkflowInstance>> GetByEntityAsync(string entityId, string entityType)
    {
        return await _context.WorkflowInstances
            .Include(w => w.Steps)
            .Where(w => w.EntityId == entityId && w.EntityType == entityType)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task AddCommentAsync(string workflowId, int commenterUserId, string commenterName, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Comment cannot be empty.", nameof(comment));

        var workflow = await _context.WorkflowInstances
            .FirstOrDefaultAsync(w => w.Id == workflowId)
            ?? throw new KeyNotFoundException($"Workflow {workflowId} not found.");

        if (workflow.ApproverUserId != commenterUserId)
            throw new UnauthorizedAccessException("Only the assigned approver may comment on a pending workflow.");

        _context.WorkflowSteps.Add(new WorkflowStep
        {
            WorkflowInstanceId = workflow.Id,
            StepLevel = workflow.CurrentLevel,
            ApproverUserId = commenterUserId,
            ApproverName = commenterName,
            Action = "Comment",
            Comments = comment,
            ActionDate = DateTime.UtcNow
        });

        workflow.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
