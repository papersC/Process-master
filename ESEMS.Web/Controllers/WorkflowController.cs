using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Common;
using ESEMS.Web.Services.Improvements;
using ESEMS.Web.Services.Workflow;

namespace ESEMS.Web.Controllers;

// Class-level gate is CanApprove (any *.Approve claim or *.*), not Workflow.View.
// PendingApprovals is the approver inbox; the per-action [Approve|Reject|Reassign]
// endpoints already require module-specific .Approve. Gating page entry on .View
// leaked the page (rendering an empty queue) to every Viewer-only persona.
//
// We use the broad CanApprove policy rather than Module.Workflow.Approve so that
// module-specific approvers (Quality Officer with Improvement.Approve, Risk
// Manager with Risk.Approve) can still see their filtered queue without needing
// the catch-all Workflow.Approve permission. Personas with no .Approve claim
// (Viewer, Editor, Process Owner, Improvement Analyst) → 403, as the spec.
[Authorize(Policy = AppPolicies.CanApprove)]
public class WorkflowController : BaseController
{
    private readonly IWorkflowService _workflowService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkflowController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IScopingService _scopingService;

    public WorkflowController(IWorkflowService workflowService, ApplicationDbContext context, ILogger<WorkflowController> logger, IStringLocalizer<SharedResource> localizer, IScopingService scopingService)
    {
        _workflowService = workflowService;
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _scopingService = scopingService;
    }

    // /Workflow → pending approvals; the sidebar links to the sub-action.
    public IActionResult Index() => RedirectToAction(nameof(PendingApprovals));

    public async Task<IActionResult> PendingApprovals()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return RedirectToAction("Login", "Account");

        var pending = await _workflowService.GetPendingApprovalsAsync(userId);

        // For Improvement rows, pre-load the linked initiatives so the view
        // can render the inline "Re-rank…" modal with the current Quadrant +
        // Horizon pre-selected — without this the modal would have to do an
        // extra round trip per click.
        var improvementIds = pending
            .Where(w => string.Equals(w.EntityType, "Improvement", StringComparison.OrdinalIgnoreCase))
            .Select(w => w.EntityId)
            .Distinct()
            .ToList();

        var improvementsById = improvementIds.Count == 0
            ? new Dictionary<string, Models.Improvement.ImprovementInitiative>()
            : await _context.ImprovementInitiatives
                .Where(i => improvementIds.Contains(i.Id) && !i.IsDeleted)
                .ToDictionaryAsync(i => i.Id);

        ViewBag.ImprovementsById = improvementsById;

        return View(pending);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Workflow.Approve)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessAction(string workflowId, string action, string? comments)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userName = User.Identity?.Name ?? "Unknown";

            // F-009 + Audit C4 fix: wrap the user-initiated transaction in an
            // execution strategy. The DbContext has EnableRetryOnFailure
            // configured (Program.cs UseSqlServer), and EF Core's
            // SqlServerRetryingExecutionStrategy rejects raw BeginTransaction
            // calls — every Approve/Reject was failing with "does not support
            // user-initiated transactions" at the production-load mark. The
            // strategy delegate is the unit of retry: load workflow → process
            // action → back-sync linked entity → commit, all in one wrap so a
            // transient failure rolls back cleanly and re-runs.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Look up the workflow inside the strategy so a retry gets a
                // fresh read after a transient failure.
                var workflow = await _workflowService.GetByIdAsync(workflowId);

                using var tx = await _context.Database.BeginTransactionAsync();

                await _workflowService.ProcessActionAsync(workflowId, userId, userName, action, comments);

                // For Improvement workflows, map the workflow action onto the
                // linked initiative's Status field so the two stay in sync.
                // Approvers who come from /Workflow/PendingApprovals need this —
                // otherwise the workflow would be Approved but the initiative
                // would still show UnderReview.
                if (workflow != null && string.Equals(workflow.EntityType, "Improvement", StringComparison.OrdinalIgnoreCase))
                {
                    var improvement = await _context.ImprovementInitiatives
                        .FirstOrDefaultAsync(i => i.Id == workflow.EntityId && !i.IsDeleted);
                    if (improvement != null)
                    {
                        ImprovementStatus? target = action switch
                        {
                            "Approved" => ImprovementStatus.Approved,
                            "Rejected" => ImprovementStatus.Rejected,
                            "ReturnedForCorrection" => ImprovementStatus.Proposed,
                            _ => null
                        };
                        if (target.HasValue && ImprovementStatusMachine.CanTransition(improvement.Status, target.Value))
                        {
                            improvement.Status = target.Value;
                            improvement.UpdatedAt = DateTime.UtcNow;
                            improvement.UpdatedById = userName;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                // FLOW-002: ChangeRequest workflows acted on from the unified inbox
                // need the same back-sync — otherwise the workflow row flips to
                // Approved/Rejected here while the ChangeRequest.Status remains
                // Submitted/UnderReview, leaving the two views contradicting each
                // other. Mirror the action onto the CR's own status field.
                else if (workflow != null && string.Equals(workflow.EntityType, "ChangeRequest", StringComparison.OrdinalIgnoreCase))
                {
                    var cr = await _context.ChangeRequests
                        .FirstOrDefaultAsync(c => c.Id == workflow.EntityId && !c.IsDeleted);
                    if (cr != null)
                    {
                        Models.Enums.ChangeRequestStatus? crTarget = action switch
                        {
                            "Approved" => Models.Enums.ChangeRequestStatus.Approved,
                            "Rejected" => Models.Enums.ChangeRequestStatus.Rejected,
                            // "Returned for correction" rolls the CR back to its
                            // editable Submitted state (CR has no Proposed state).
                            "ReturnedForCorrection" => Models.Enums.ChangeRequestStatus.Submitted,
                            _ => null
                        };
                        // Only sync from an actionable (still-open) CR state so we
                        // don't resurrect/overwrite an already-terminal request.
                        var crActionable = cr.Status == Models.Enums.ChangeRequestStatus.Submitted
                                        || cr.Status == Models.Enums.ChangeRequestStatus.UnderReview;
                        if (crTarget.HasValue && crActionable)
                        {
                            cr.Status = crTarget.Value;
                            if (crTarget.Value == Models.Enums.ChangeRequestStatus.Approved)
                            {
                                cr.ApprovedById = userName;
                                cr.ApprovalDate = DateTime.UtcNow;
                            }
                            else if (crTarget.Value == Models.Enums.ChangeRequestStatus.Rejected)
                            {
                                cr.RejectionReason = comments;
                            }
                            cr.UpdatedAt = DateTime.UtcNow;
                            cr.UpdatedById = userName;
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                // F-009: commit the workflow decision and the linked-entity status
                // back-sync together. Both wrote through the same request-scoped
                // DbContext under one transaction, so this single commit makes the
                // pair atomic — no more "workflow Approved but entity still
                // UnderReview" if the second write had failed.
                await tx.CommitAsync();
            });

            TempData["Success"] = action switch
            {
                "Approved" => _localizer["Success_RequestApproved"].Value,
                "Rejected" => _localizer["Success_RequestRejected"].Value,
                "ReturnedForCorrection" => _localizer["Success_RequestReturned"].Value,
                _ => _localizer["Success_RequestActionProcessed"].Value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing workflow action");
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(PendingApprovals));
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Workflow.Approve)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(string workflowId, string comment)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userName = User.Identity?.Name ?? "Unknown";
            await _workflowService.AddCommentAsync(workflowId, userId, userName, comment);
            TempData["Success"] = "Comment added.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding workflow comment");
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(PendingApprovals));
    }

    public async Task<IActionResult> Details(string id)
    {
        var workflow = await _workflowService.GetByIdAsync(id);
        if (workflow == null) return NotFound();

        // FLOW-006 (IDOR): this action only required the Workflow.View policy,
        // so any authenticated viewer could read ANY workflow — including the
        // submitter's identity, approver chain, and free-text comments — just by
        // guessing/iterating ids. Restrict to people with a legitimate
        // relationship to this workflow:
        //   • the submitter,
        //   • the current assigned approver, or any historical/step approver,
        //   • a user whose data-visibility scope covers the linked entity.
        var userId = GetCurrentUserId();
        var isParty = (workflow.SubmittedById.HasValue && workflow.SubmittedById.Value == userId)
                   || (workflow.ApproverUserId.HasValue && workflow.ApproverUserId.Value == userId)
                   || workflow.Steps.Any(s => s.ApproverUserId == userId
                                           || s.DelegatedFromUserId == userId);

        if (!isParty)
        {
            // Fall back to entity-scope: if the viewer can see the underlying
            // Improvement / ChangeRequest, they may see its workflow too.
            var scope = await _scopingService.GetScopeAsync(User);
            var inScope = await IsEntityInScopeAsync(workflow.EntityType, workflow.EntityId, scope);
            if (!inScope) return NotFound();
        }

        return View(workflow);
    }

    /// <summary>
    /// FLOW-006 helper: is the workflow's linked entity within the caller's
    /// data-visibility scope? Only the two entity types that currently drive
    /// workflows (Improvement, ChangeRequest) are resolvable; anything else is
    /// treated as out-of-scope so unknown entity types fail closed.
    /// </summary>
    private async Task<bool> IsEntityInScopeAsync(string entityType, string entityId, ScopeContext scope)
    {
        if (string.IsNullOrWhiteSpace(entityId)) return false;

        if (string.Equals(entityType, "Improvement", StringComparison.OrdinalIgnoreCase))
        {
            var imp = await _context.ImprovementInitiatives
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == entityId && !i.IsDeleted);
            return imp != null && scope.CanAccess(imp);
        }
        if (string.Equals(entityType, "ChangeRequest", StringComparison.OrdinalIgnoreCase))
        {
            var cr = await _context.ChangeRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == entityId && !c.IsDeleted);
            return cr != null && scope.CanAccess(cr);
        }
        return false;
    }

    /// <summary>
    /// Small picker-friendly list of active users — feeds the delegation
    /// modal on PendingApprovals. Not admin-only because any approver
    /// needs to see potential delegation targets.
    /// </summary>
    [HttpGet("/api/users/active")]
    [Authorize]
    public async Task<IActionResult> ActiveUsers()
    {
        var self = GetCurrentUserId();
        var users = await _context.CustomUsers
            .Where(u => u.UserId != self)
            .OrderBy(u => u.EmployeeName ?? u.Username)
            .Select(u => new {
                userId = u.UserId,
                username = u.Username,
                fullName = u.FullName,
                fullNameEn = u.EmployeeName ?? u.FullName ?? u.Username,
                fullNameAr = u.EmployeeNameAr,
                email = u.EmailAddress,
                deptEn = u.Department ?? u.DirectOrgNameEn,
                deptAr = u.DepartmentAr ?? u.DirectOrgNameAr
            })
            .Take(500)
            .ToListAsync();
        return Json(users);
    }

    /// <summary>
    /// Delegate a pending approval to another user. Swaps the step's
    /// ApproverUserId, records the original approver + optional expiry so
    /// the SLA worker can bounce it back automatically, and appends an
    /// audit trail to Comments.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Workflow.Approve)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delegate(string workflowId, int targetUserId, string? reason, DateTime? expiresAt)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var step = await _context.WorkflowSteps
            .Include(s => s.WorkflowInstance)
            .FirstOrDefaultAsync(s => s.WorkflowInstanceId == workflowId
                                   && s.ApproverUserId == userId
                                   && s.Action == "Pending");
        if (step == null)
        {
            TempData["Error"] = _localizer["Error_NoPendingStepDelegate"].Value;
            return RedirectToAction(nameof(PendingApprovals));
        }

        var target = await _context.CustomUsers
            .FirstOrDefaultAsync(u => u.UserId == targetUserId);
        if (target == null)
        {
            TempData["Error"] = _localizer["Error_TargetUserNotFound"].Value;
            return RedirectToAction(nameof(PendingApprovals));
        }

        var originalApproverName = step.ApproverName ?? User.Identity?.Name ?? "Unknown";
        // Only capture the original approver on the first delegation so
        // serial re-delegations still bounce back to the true owner.
        if (step.DelegatedFromUserId == null)
        {
            step.DelegatedFromUserId = step.ApproverUserId;
            step.DelegatedFromName = originalApproverName;
        }
        step.DelegationExpiresAt = expiresAt?.ToUniversalTime();

        var expiryNote = expiresAt.HasValue ? $", returns on {expiresAt.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC" : "";
        var trail = $"[Delegated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC] from {originalApproverName} to {target.FullName ?? target.Username}{expiryNote}" +
                    (string.IsNullOrWhiteSpace(reason) ? "" : $" — reason: {reason}");
        step.Comments = string.IsNullOrWhiteSpace(step.Comments) ? trail : $"{step.Comments}\n{trail}";
        step.ApproverUserId = targetUserId;
        step.ApproverName = target.FullName ?? target.Username;

        // Also update the parent WorkflowInstance so PendingApprovals picks
        // up the new approver immediately.
        if (step.WorkflowInstance != null && step.StepLevel == step.WorkflowInstance.CurrentLevel)
            step.WorkflowInstance.ApproverUserId = targetUserId;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Delegated to {step.ApproverName}" + (expiresAt.HasValue ? $" until {expiresAt.Value:yyyy-MM-dd}" : "") + ".";
        _logger.LogInformation("Workflow {WorkflowId} step delegated from user {From} to user {To} expiresAt={Expiry}", workflowId, userId, targetUserId, expiresAt);
        return RedirectToAction(nameof(PendingApprovals));
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }
}
