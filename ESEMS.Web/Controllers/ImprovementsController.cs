using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.ViewModels;
using ESEMS.Web.Models.Workflow;
using ESEMS.Web.Security;
using ESEMS.Web.Extensions;
using ESEMS.Web.Services.Common;
using ESEMS.Web.Services.Export;
using ESEMS.Web.Services.Improvements;
using ESEMS.Web.Services.Notifications;
using ESEMS.Web.Services.Workflow;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for Improvement Initiatives management
/// </summary>
[Authorize(Policy = AppPolicies.Module.Improvement.View)]
public class ImprovementsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ImprovementsController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IImprovementWorkflowService _improvementService;
    private readonly IWorkflowService _workflowService;
    private readonly INotificationService _notificationService;
    private readonly IMeasurementCollectionService _measurementCollection;
    private readonly IScopingService _scopingService;
    private readonly HierarchicalCodeService _codeSvc;
    private readonly IExportService _exportSvc;

    public ImprovementsController(
        ApplicationDbContext context,
        ILogger<ImprovementsController> logger,
        IStringLocalizer<SharedResource> localizer,
        IImprovementWorkflowService improvementService,
        IWorkflowService workflowService,
        INotificationService notificationService,
        IMeasurementCollectionService measurementCollection,
        IScopingService scopingService,
        HierarchicalCodeService codeSvc,
        IExportService exportSvc)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _improvementService = improvementService;
        _workflowService = workflowService;
        _notificationService = notificationService;
        _measurementCollection = measurementCollection;
        _scopingService = scopingService;
        _codeSvc = codeSvc;
        _exportSvc = exportSvc;
    }

    /// <summary>
    /// Loads the active <see cref="PrioritizationConfig"/> (audit #17) so
    /// <see cref="ImprovementInitiative.CalculateQuadrant"/> can be called
    /// with the configured cut-offs. Falls back to the historical 6 / 6
    /// values when no row is active so legacy data still renders identically.
    /// </summary>
    private async Task<(int impactCutoff, int effortCutoff)> GetQuadrantCutoffsAsync()
    {
        var config = await _context.PrioritizationConfigs
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync();
        return config is null ? (6, 6) : (config.ImpactCutoff, config.EffortCutoff);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    private string GetCurrentUserName() => User.Identity?.Name ?? "System";

    /// <summary>
    /// Loads an initiative by id or returns NotFound.
    /// </summary>
    private async Task<ImprovementInitiative?> FindInitiativeAsync(string id)
    {
        return await _context.ImprovementInitiatives
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
    }

    /// <summary>
    /// Notifies the initiative owner when the initiative is created.
    /// Skipped when no owner is set or when the owner is also the creator.
    ///
    /// Audit #18: the previous "Innovation Registry" notification (which
    /// pinged every ADMIN whenever an initiative was tagged Breakthrough or
    /// Transformative) has been removed. There is no Innovation Registry
    /// entity backing the notification, so the alert pointed nowhere and
    /// produced noise. Re-introduce it only after a real
    /// InnovationRegistryEntry table exists with status workflow.
    /// </summary>
    private async Task NotifyOwnerOfCreationAsync(ImprovementInitiative improvement, int createdById)
    {
        if (!string.IsNullOrWhiteSpace(improvement.OwnerId)
            && int.TryParse(improvement.OwnerId, out var ownerId)
            && ownerId != 0
            && ownerId != createdById)
        {
            await _notificationService.SendAsync(ownerId,
                "New improvement assigned to you",
                "تم تعيين مبادرة تحسين جديدة لك",
                $"You have been assigned as owner of initiative '{improvement.TitleEn}'.",
                $"تم تعيينك مالكاً لمبادرة '{improvement.TitleAr}'.",
                "Info",
                improvement.Id,
                "Improvement",
                $"/Improvements/Details/{improvement.Id}");
        }
    }

    /// <summary>
    /// Improvement Journey Dashboard - Landing page explaining the 5-step journey
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        var metrics = await _improvementService.CalculateDashboardMetricsAsync();

        ViewBag.TotalInitiatives = metrics.TotalInitiatives;
        ViewBag.QuickWinsCount = metrics.QuickWinsCount;
        ViewBag.MajorProjectsCount = metrics.MajorProjectsCount;
        ViewBag.FillInsCount = metrics.FillInsCount;
        ViewBag.ThanklessTasksCount = metrics.ThanklessTasksCount;
        ViewBag.H1Count = metrics.H1Count;
        ViewBag.H2Count = metrics.H2Count;
        ViewBag.H3Count = metrics.H3Count;

        return View(metrics.TopPriorities);
    }

    /// <summary>
    /// List all improvement initiatives
    /// </summary>
    public async Task<IActionResult> Index(ImprovementQuadrant? quadrant = null, string? status = null, string? statusFilter = null)
    {
        // statusFilter is the long-form alias used by notification deep-links
        // (see InitiativeStallDetectionService). Treat it as the same input as
        // the `status` query param so stalled-initiative notifications actually
        // narrow the list instead of being silently dropped.
        var effectiveStatus = !string.IsNullOrEmpty(status) ? status : statusFilter;

        var query = _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted)
	            .Include(i => i.ImprovementProcesses)
	                .ThenInclude(ip => ip.Process)
	            .Include(i => i.ImprovementServices)
	                .ThenInclude(isv => isv.Service)
            .Include(i => i.Process)
            .Include(i => i.Service)
            .Include(i => i.OwningUnit)
            .AsQueryable();

        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyOwningUnitScope(scope);

        if (quadrant.HasValue)
            query = query.Where(i => i.Quadrant == quadrant.Value);

        // "Stalled" is a synthetic status (no UpdatedAt for 90+ days while still
        // in an active state) — it's what InitiativeStallDetectionService surfaces
        // in notifications, but it isn't an ImprovementStatus enum value. Apply
        // the time-based predicate explicitly when the notification deep-links in.
        var isStalledFilter = string.Equals(effectiveStatus, "Stalled", StringComparison.OrdinalIgnoreCase);
        if (isStalledFilter)
        {
            var stallCutoff = DateTime.UtcNow.AddDays(-90);
            query = query.Where(i =>
                i.UpdatedAt < stallCutoff &&
                (i.Status == ImprovementStatus.Proposed
                  || i.Status == ImprovementStatus.UnderReview
                  || i.Status == ImprovementStatus.Approved
                  || i.Status == ImprovementStatus.InProgress
                  || i.Status == ImprovementStatus.OnHold));
        }
        else if (!string.IsNullOrEmpty(effectiveStatus)
              && Enum.TryParse<ImprovementStatus>(effectiveStatus, ignoreCase: true, out var statusFilterValue))
        {
            query = query.Where(i => i.Status == statusFilterValue);
        }

        var improvements = await query.OrderBy(i => i.Priority)
            .ThenByDescending(i => i.CreatedAt)
            .ToListAsync();

        // PERF/SCOPE: quadrant counts for the filter cards. Was a full-table
        // load + in-memory Count() that also ignored the caller's scope (a
        // scoped user saw org-wide counts that didn't match the scoped list
        // below — a minor info leak + UX inconsistency). Now a single
        // scope-filtered SQL GROUP BY.
        var quadrantCounts = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted)
            .ApplyOwningUnitScope(scope)
            .GroupBy(i => i.Quadrant)
            .Select(g => new { Quadrant = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Quadrant, x => x.Count);

        ViewBag.QuickWinsCount = quadrantCounts.GetValueOrDefault(ImprovementQuadrant.QuickWins);
        ViewBag.MajorProjectsCount = quadrantCounts.GetValueOrDefault(ImprovementQuadrant.MajorProjects);
        ViewBag.FillInsCount = quadrantCounts.GetValueOrDefault(ImprovementQuadrant.FillIns);
        ViewBag.ThanklessTasksCount = quadrantCounts.GetValueOrDefault(ImprovementQuadrant.ThanklessTasks);

        ViewBag.SelectedQuadrant = quadrant;
        ViewBag.SelectedStatus = effectiveStatus;
        ViewBag.IsStalledFilter = isStalledFilter;
        return View(improvements);
    }

    /// <summary>
    /// View improvement details
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var improvement = await _context.ImprovementInitiatives
	            .Include(i => i.ImprovementProcesses)
	                .ThenInclude(ip => ip.Process)
            .Include(i => i.Process)
                .ThenInclude(p => p!.ProcessGroup)
	            .Include(i => i.ImprovementServices)
	                .ThenInclude(isv => isv.Service)
            .Include(i => i.Service)
            .Include(i => i.OwningUnit)
            .Include(i => i.Actions.Where(a => !a.IsDeleted))
            .Include(i => i.Measurements.Where(m => !m.IsDeleted && m.IsActive))
            .Include(i => i.TeamMembers.Where(tm => tm.IsActive))
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        if (improvement == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(improvement))
            return NotFound();

        // Get all users for team assignment dropdown
        ViewBag.AllUsers = await _context.Users
            .OrderBy(u => u.EmployeeName)
            .Select(u => new SelectListItem
            {
                Value = u.UserId.ToString(),
                Text = u.EmployeeName ?? u.FullName ?? u.Username
            })
            .ToListAsync();

        // Load the closure report if one exists — used by the Details view
        // to render the "Closed" banner with lessons learned and actual-vs-
        // estimated savings.
        ViewBag.ClosureReport = await _context.ImprovementClosureReports
            .FirstOrDefaultAsync(r => r.ImprovementId == id);

        // Load stage-gate reviews, newest first, for the Reviews tab.
        ViewBag.Reviews = await _context.ImprovementReviews
            .Where(r => r.ImprovementId == id)
            .OrderByDescending(r => r.ReviewDate)
            .ToListAsync();

        // FLOW-005: the Approve / Reject / Return buttons must only render for
        // the user actually assigned to the active approval workflow — not for
        // anyone who merely holds the Improvement.Approve policy. Resolve the
        // open workflow and compare its ApproverUserId to the current user.
        // This flag GATES the rendering of those buttons in the view; the
        // server-side actions enforce assignment independently (defence in
        // depth), so a bypassed UI still can't approve.
        var currentUserId = GetCurrentUserId();
        var activeWorkflow = (await _workflowService.GetByEntityAsync(id, "Improvement"))
            .FirstOrDefault(w => w.Status == WorkflowStatus.Submitted || w.Status == WorkflowStatus.UnderReview);
        ViewBag.IsAssignedApprover = activeWorkflow != null
            && currentUserId != 0
            && activeWorkflow.ApproverUserId == currentUserId;

        return View(improvement);
    }

    /// <summary>
    /// Edit improvement form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var improvement = await _context.ImprovementInitiatives.FindAsync(id);
        if (improvement == null || improvement.IsDeleted)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(improvement))
            return NotFound();

        await PopulateDropdowns();
        return View(improvement);
    }

    /// <summary>
    /// Edit improvement
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ImprovementInitiative improvement)
    {
        if (id != improvement.Id)
            return NotFound();

        // FUNC-014 (mass-assignment): load the TRACKED entity and patch only
        // user-editable descriptive/planning fields. The previous code did a
        // blanket _context.Update(improvement) on the model-bound instance,
        // which let a crafted POST overwrite Status (bypassing the approval
        // lifecycle), IsDeleted (un/soft-deleting at will), Code, and the audit
        // stamps. Status transitions now ONLY happen through the lifecycle
        // endpoints (Submit/Approve/Reject/Return/Transition/Close).
        var entity = await _context.ImprovementInitiatives
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (entity == null) return NotFound();

        // IDOR defense: the caller must be able to access the entity as it is
        // CURRENTLY persisted (its existing OwningUnit), not as posted.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(entity))
            return NotFound();

        // Scope-escape defense: if the unit is being reassigned, the NEW unit
        // must also be within the caller's scope — otherwise a user could push
        // an initiative into a unit they don't control (or out of their own).
        if (improvement.OwningUnitId != entity.OwningUnitId
            && !scope.CanAccess(new ImprovementScopeProbe(improvement.OwningUnitId)))
        {
            var isAr = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
            ModelState.AddModelError(nameof(ImprovementInitiative.OwningUnitId),
                isAr
                    ? "لا يمكنك نقل المبادرة إلى وحدة خارج نطاق صلاحياتك."
                    : "You cannot reassign this initiative to a unit outside your scope.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                // ── Copy ONLY user-editable fields ───────────────────────────
                // Descriptive
                entity.TitleEn       = improvement.TitleEn;
                entity.TitleAr       = improvement.TitleAr;
                entity.NameEn        = improvement.NameEn;
                entity.NameAr        = improvement.NameAr;
                entity.DescriptionEn = improvement.DescriptionEn;
                entity.DescriptionAr = improvement.DescriptionAr;
                // Classification / planning
                entity.Source                = improvement.Source;
                entity.Priority              = improvement.Priority;
                entity.ProcessId             = improvement.ProcessId;
                entity.ServiceId             = improvement.ServiceId;
                entity.OwnerId               = improvement.OwnerId;
                entity.OwningUnitId          = improvement.OwningUnitId;
                entity.ImpactScore           = improvement.ImpactScore;
                entity.EffortScore           = improvement.EffortScore;
                entity.EstimatedCostSavings  = improvement.EstimatedCostSavings;
                entity.EstimatedTimeSavings  = improvement.EstimatedTimeSavings;
                entity.ProgressPercentage    = improvement.ProgressPercentage;
                entity.TargetDate            = improvement.TargetDate;
                entity.Horizon               = improvement.Horizon;
                entity.InnovationType        = improvement.InnovationType;
                entity.ExternalReferenceId   = improvement.ExternalReferenceId;
                // NOTE: deliberately NOT copied — Status, IsDeleted, Code,
                // CompletedDate, CreatedAt/ById (system/lifecycle-owned).

                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedById = User.Identity?.Name;
                var (impactCutoff, effortCutoff) = await GetQuadrantCutoffsAsync();
                entity.CalculateQuadrant(impactCutoff, effortCutoff);

                // Preserve optimistic concurrency: pin the original Version to
                // the value the form was rendered with, then bump. A stale POST
                // (Tab B saving after Tab A bumped Version) matches 0 rows and
                // throws DbUpdateConcurrencyException instead of silently
                // clobbering the newer edit.
                _context.Entry(entity).Property(e => e.Version).OriginalValue = improvement.Version;
                entity.Version = improvement.Version + 1;
                await _context.SaveChangesAsync();

                TempData["Success"] = _localizer["Success_ImprovementUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id = entity.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another user saved this record after we loaded it. Don't silently
                // overwrite — surface a warning and re-render the form so the user
                // can re-apply their edits against the latest version.
                if (!await ImprovementExists(improvement.Id))
                    return NotFound();

                ModelState.AddModelError(string.Empty, _localizer["Error_ConcurrentEdit"].Value);
                TempData["Error"] = _localizer["Error_ConcurrentEdit"].Value;
            }
        }

        await PopulateDropdowns();
        return View(improvement);
    }

    private Task<bool> ImprovementExists(string id) =>
        _context.ImprovementInitiatives.AnyAsync(i => i.Id == id && !i.IsDeleted);

    /// <summary>
    /// Kanban board view
    /// </summary>
    public async Task<IActionResult> Kanban()
    {
        // FLOW-010: apply the same owning-unit data-visibility scope that
        // Index uses. Without it the Kanban board leaked initiatives from
        // units the user has no rights to see.
        var query = _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted)
            .Include(i => i.Process)
            .Include(i => i.Service)
            .AsQueryable();

        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyOwningUnitScope(scope);

        var improvements = await query
            .OrderBy(i => i.Priority)
            .ToListAsync();

        return View(improvements);
    }

    /// <summary>
    /// Update improvement status (for Kanban drag-and-drop)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest request)
    {
        try
        {
            var improvement = await _context.ImprovementInitiatives.FindAsync(request.Id);
            if (improvement == null || improvement.IsDeleted)
                return Json(new { success = false, error = "Improvement not found" });

            // IDOR guard: a Kanban drag is just a POST — verify the caller's
            // scope covers this initiative before mutating it.
            if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement))
                return Json(new { success = false, error = "Improvement not found" });

            if (!Enum.TryParse<ImprovementStatus>(request.Status, ignoreCase: true, out var targetStatus))
                return Json(new { success = false, error = $"Unknown status '{request.Status}'." });

            // FLOW-011: the Kanban board must not be an approval back-door.
            // Moving a card INTO Approved or Rejected is a governance decision
            // that has to run through the approval workflow (Submit → assigned
            // approver → Approve/Reject) so the WorkflowInstance, approver
            // assignment, audit steps, and self-approval / dual-level guards
            // all apply. Block those target states here regardless of what the
            // raw FSM would otherwise permit.
            var ar = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            if (targetStatus is ImprovementStatus.Approved or ImprovementStatus.Rejected)
            {
                return Json(new
                {
                    success = false,
                    error = ar
                        ? $"لا يمكن تعيين الحالة '{targetStatus}' إلا من خلال مسار الاعتماد، وليس بالسحب على اللوحة. استخدم «إرسال» ثم اعتمدها من صندوق الاعتمادات."
                        : $"'{targetStatus}' can only be set through the approval workflow, not by dragging on the board. Use Submit, then approve from the approvals inbox."
                });
            }

            // Validate the transition against the state machine so Kanban
            // drag-and-drop can't produce illegal states. Include the list
            // of legal next states in the error so the UI can tell the user
            // exactly which columns they CAN drop into, instead of just
            // "nope".
            if (!ImprovementStatusMachine.CanTransition(improvement.Status, targetStatus))
            {
                var allowed = ImprovementStatusMachine.AllowedNext(improvement.Status);
                var allowedText = allowed.Any() ? string.Join(", ", allowed) : "(none — terminal state)";
                return Json(new
                {
                    success = false,
                    error = ar
                        ? $"لا يمكن النقل من '{improvement.Status}' إلى '{request.Status}'. الانتقالات المسموحة: {allowedText}."
                        : $"Cannot move from '{improvement.Status}' to '{request.Status}'. Allowed transitions from '{improvement.Status}': {allowedText}."
                });
            }

            improvement.Status = targetStatus;
            improvement.UpdatedAt = DateTime.UtcNow;
            improvement.UpdatedById = User.Identity?.Name;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating improvement status");
            return Json(new { success = false, error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Lifecycle transitions — Submit / Approve / Reject / Return / Start /
    // Pause / Resume / Cancel / Archive
    //
    // Each action:
    //   1. Validates the transition via ImprovementStatusMachine
    //   2. Calls IWorkflowService where an approval workflow is involved
    //   3. Mutates ImprovementInitiative.Status
    //   4. The WorkflowService fires notifications for approval steps;
    //      these endpoints fire direct notifications for non-approval
    //      transitions (Start / Pause / Resume).
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Submit a Proposed or Rejected initiative for approval.
    /// Creates a WorkflowInstance and transitions the initiative to UnderReview.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> Submit(string id, string? notes = null)
    {
        var improvement = await FindInitiativeAsync(id);
        if (improvement == null) return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement)) return NotFound();

        ImprovementStatusMachine.EnsureTransition(improvement.Status, ImprovementStatus.UnderReview);

        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        // Build the approval context so the WorkflowService can pick the
        // matching rule from ApprovalConfigurations (budget tier, horizon,
        // innovation type, duration, impact). First matching rule wins.
        var durationDays = improvement.TargetDate.HasValue
            ? (int?)Math.Max(0, (improvement.TargetDate.Value - improvement.CreatedAt).Days)
            : null;
        var ctx = new ESEMS.Web.Models.Workflow.ApprovalContext
        {
            CostSavings = improvement.EstimatedCostSavings,
            ImpactScore = improvement.ImpactScore,
            DurationDays = durationDays,
            Horizon = improvement.Horizon?.ToString(),
            InnovationType = improvement.InnovationType?.ToString()
        };
        await _workflowService.CreateAsync(improvement.Id, "Improvement", userId, userName, notes, ctx);

        improvement.Status = ImprovementStatus.UnderReview;
        improvement.UpdatedAt = DateTime.UtcNow;
        improvement.UpdatedById = userName;
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_ImprovementSubmitted"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Approve an initiative currently UnderReview. Resolves the open
    /// workflow instance and asks the workflow service to record the
    /// "Approved" action (which also fires the success notification to
    /// the submitter).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Approve)]
    public async Task<IActionResult> Approve(string id, string? comments = null, ImprovementQuadrant? quadrant = null, ImprovementHorizon? horizon = null)
    {
        var improvement = await FindInitiativeAsync(id);
        if (improvement == null) return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement)) return NotFound();

        ImprovementStatusMachine.EnsureTransition(improvement.Status, ImprovementStatus.Approved);

        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        var workflow = (await _workflowService.GetByEntityAsync(id, "Improvement"))
            .FirstOrDefault(w => w.Status == WorkflowStatus.Submitted || w.Status == WorkflowStatus.UnderReview);
        if (workflow == null) return BadRequest("No active workflow for this initiative.");

        // FLOW-001 (self-approval): block early with a clean message rather than
        // letting ProcessActionAsync throw (which would surface as a 500). The
        // engine still enforces this centrally as defence-in-depth.
        if (workflow.SubmittedById == userId && userId != 0)
        {
            TempData["Error"] = "You cannot approve an initiative that you submitted. Approval must be performed by a different user.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Approver re-rank: the approver can override the auto-calculated
        // Quadrant (Quick Wins / Major Projects / Fill-Ins / Avoid) and the
        // Horizon (Now / Expand / Future) before approving so the final
        // classification reflects committee judgment, not just the slider
        // formula. Each override gets its own ImprovementChangeLog entry so
        // the audit trail makes it clear what changed and why.
        if (quadrant.HasValue && quadrant.Value != improvement.Quadrant)
        {
            var oldQuadrant = improvement.Quadrant;
            improvement.Quadrant = quadrant.Value;
            _context.Set<Models.Improvement.ImprovementChangeLog>().Add(new Models.Improvement.ImprovementChangeLog
            {
                Id = Guid.NewGuid().ToString(),
                ImprovementId = improvement.Id,
                FieldName = nameof(ImprovementInitiative.Quadrant),
                OldValue = oldQuadrant.ToString(),
                NewValue = quadrant.Value.ToString(),
                ChangeReason = $"Re-classified by approver during approval. {comments}".Trim(),
                ChangedById = userName,
                ChangedAt = DateTime.UtcNow
            });
        }
        if (horizon.HasValue && horizon.Value != improvement.Horizon)
        {
            var oldHorizon = improvement.Horizon;
            improvement.Horizon = horizon.Value;
            _context.Set<Models.Improvement.ImprovementChangeLog>().Add(new Models.Improvement.ImprovementChangeLog
            {
                Id = Guid.NewGuid().ToString(),
                ImprovementId = improvement.Id,
                FieldName = nameof(ImprovementInitiative.Horizon),
                OldValue = oldHorizon?.ToString() ?? "(unset)",
                NewValue = horizon.Value.ToString(),
                ChangeReason = $"Re-classified by approver during approval. {comments}".Trim(),
                ChangedById = userName,
                ChangedAt = DateTime.UtcNow
            });
        }

        // FLOW-007 (atomicity): mutate the initiative on the SAME tracked
        // context BEFORE invoking the engine. ProcessActionAsync performs a
        // single SaveChangesAsync that now flushes the workflow rows, the
        // re-rank change-logs, AND the initiative status together — so the
        // approval can no longer half-commit (workflow advanced but initiative
        // status not, or vice-versa). Notifications fire only after that commit
        // succeeds (FLOW-008).
        //
        // Only promote the initiative to Approved when THIS action is the final
        // approval. For a two-level rule, an L1 approval advances the workflow
        // to L2 (workflow stays UnderReview) — the initiative must stay
        // UnderReview too until L2 signs off, otherwise it would show Approved
        // while a second approver is still pending.
        var isFinalApproval = workflow.CurrentLevel >= workflow.MaxLevel;
        if (isFinalApproval)
        {
            improvement.Status = ImprovementStatus.Approved;
            improvement.UpdatedAt = DateTime.UtcNow;
            improvement.UpdatedById = userName;
        }
        try
        {
            await _workflowService.ProcessActionAsync(workflow.Id, userId, userName, "Approved", comments);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            // Governance guard tripped (self-approval / dual-level one-person
            // clearance) or the caller isn't the assigned approver. Nothing was
            // committed; surface a clean message instead of a 500.
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = _localizer["Success_ImprovementApproved"].Value;
        // If the approver came from the unified inbox, return them there to
        // continue the batch instead of forcing a back-button click.
        var returnTo = Request.Form["returnTo"].ToString();
        if (returnTo == "queue") return RedirectToAction("PendingApprovals", "Workflow");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Reject an initiative currently UnderReview.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Approve)]
    public async Task<IActionResult> Reject(string id, string? comments = null)
    {
        var improvement = await FindInitiativeAsync(id);
        if (improvement == null) return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement)) return NotFound();

        ImprovementStatusMachine.EnsureTransition(improvement.Status, ImprovementStatus.Rejected);

        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        var workflow = (await _workflowService.GetByEntityAsync(id, "Improvement"))
            .FirstOrDefault(w => w.Status == WorkflowStatus.Submitted || w.Status == WorkflowStatus.UnderReview);
        if (workflow == null) return BadRequest("No active workflow for this initiative.");

        // FLOW-007 (atomicity): mutate the initiative on the tracked context
        // first, then let the engine's single SaveChangesAsync flush workflow +
        // initiative together so they can't half-commit.
        improvement.Status = ImprovementStatus.Rejected;
        improvement.UpdatedAt = DateTime.UtcNow;
        improvement.UpdatedById = userName;
        try
        {
            await _workflowService.ProcessActionAsync(workflow.Id, userId, userName, "Rejected", comments);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = _localizer["Success_ImprovementRejected"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Return an initiative to the submitter for correction. Status rolls
    /// back to Proposed so they can edit and submit again.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Approve)]
    public async Task<IActionResult> Return(string id, string? comments = null)
    {
        var improvement = await FindInitiativeAsync(id);
        if (improvement == null) return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement)) return NotFound();

        ImprovementStatusMachine.EnsureTransition(improvement.Status, ImprovementStatus.Proposed);

        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        var workflow = (await _workflowService.GetByEntityAsync(id, "Improvement"))
            .FirstOrDefault(w => w.Status == WorkflowStatus.Submitted || w.Status == WorkflowStatus.UnderReview);
        if (workflow == null) return BadRequest("No active workflow for this initiative.");

        // FLOW-007 (atomicity): mutate the initiative on the tracked context
        // first, then let the engine's single SaveChangesAsync flush workflow +
        // initiative together so they can't half-commit.
        improvement.Status = ImprovementStatus.Proposed;
        improvement.UpdatedAt = DateTime.UtcNow;
        improvement.UpdatedById = userName;
        try
        {
            await _workflowService.ProcessActionAsync(workflow.Id, userId, userName, "ReturnedForCorrection", comments);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = _localizer["Success_ImprovementReturned"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    // ═══════════════════════════════════════════════════════════════════
    // DGEP / 4Gen Excellence export — Pass E
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Export all closed / completed initiatives as a CSV formatted for
    /// the Dubai Government Excellence Programme (DGEP) / 4Gen annual
    /// submission. One row per initiative, including: code, title,
    /// status, innovation type, horizon, estimated vs actual savings,
    /// quadrant, total priority score, closure date, lessons learned,
    /// and the names of linked processes/services.
    ///
    /// Output is UTF-8 with BOM so Excel opens it with Arabic text
    /// correctly rendered. Content-Disposition attachment forces a
    /// download rather than an in-browser view.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.Module.Improvement.Export)]
    /// <summary>
    /// Audit #20: download the full Improvements portfolio as XLSX. Same
    /// column layout as the import template so an export-edit-reimport
    /// round-trip is non-destructive.
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Improvement.Export)]
    public async Task<IActionResult> ExportXlsx()
    {
        var scope = await _scopingService.GetScopeAsync(User);
        var bytes = await _exportSvc.ExportImprovementsToExcelAsync(scope);
        var name = $"Improvements_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    /// <summary>
    /// Audit #20: empty XLSX template the user fills in then re-uploads
    /// via <see cref="ImportXlsx"/>. Includes a Notes sheet with allowed
    /// enum values.
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Improvement.Export)]
    public IActionResult ImportTemplate()
    {
        var bytes = _exportSvc.BuildImprovementsImportTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Improvements_Import_Template.xlsx");
    }

    /// <summary>
    /// Audit #20: bulk-create initiatives from an uploaded XLSX. Codes are
    /// server-stamped (INI-{NNN}, sequential within the batch starting from
    /// the next free number).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Improvement.Create)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> ImportXlsx(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = _localizer["Error_NoFileUploaded"].Value;
            return RedirectToAction(nameof(Index));
        }
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = _localizer["Error_UploadMustBeXlsxTemplate"].Value;
            return RedirectToAction(nameof(Index));
        }

        await using var stream = file.OpenReadStream();
        var report = await _exportSvc.ImportImprovementsFromExcelAsync(stream, GetCurrentUserName());

        if (report.Errors.Count > 0)
        {
            var preview = string.Join(" · ",
                report.Errors.Take(5).Select(e => $"row {e.Row}: {e.Message}"));
            TempData["Error"] =
                $"Imported {report.Inserted} of {report.TotalRows}. {report.Errors.Count} error(s): {preview}" +
                (report.Errors.Count > 5 ? " (more rows truncated)" : "");
        }
        else
        {
            TempData["Success"] = $"Imported {report.Inserted} initiative(s) from XLSX.";
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> DgepExcellenceExport()
    {
        var scope = await _scopingService.GetScopeAsync(User);
        var initiatives = await _context.ImprovementInitiatives
            .AsNoTracking()
            .Where(i => !i.IsDeleted && (i.Status == ImprovementStatus.Completed || i.Status == ImprovementStatus.Closed))
            .ApplyOwningUnitScope(scope)
            .Include(i => i.ImprovementProcesses).ThenInclude(ip => ip.Process)
            .Include(i => i.ImprovementServices).ThenInclude(isv => isv.Service)
            .OrderByDescending(i => i.CompletedDate ?? i.UpdatedAt)
            .ToListAsync();

        var closures = await _context.ImprovementClosureReports
            .AsNoTracking()
            .ToDictionaryAsync(r => r.ImprovementId);

        var sb = new System.Text.StringBuilder();
        // UTF-8 BOM so Excel picks up the encoding automatically
        sb.Append('\uFEFF');

        // Header row matches the DGEP submission template fields
        string[] headers =
        {
            "Initiative Code",
            "Title (EN)",
            "Title (AR)",
            "Status",
            "Quadrant",
            "Horizon",
            "Innovation Type",
            "Total Priority Score",
            "Owning Unit",
            "Linked Processes",
            "Linked Services",
            "Estimated Cost Savings (AED)",
            "Actual Cost Savings (AED)",
            "Cost Savings Delta (AED)",
            "Estimated Time Savings (hours)",
            "Actual Time Savings (hours)",
            "Target Date",
            "Completed Date",
            "Lessons Learned (EN)",
            "Lessons Learned (AR)",
            "Signed Off By",
            "Closed By"
        };
        sb.AppendLine(string.Join(",", headers.Select(CsvQuote)));

        foreach (var i in initiatives)
        {
            var closure = closures.GetValueOrDefault(i.Id);
            var processes = (i.ImprovementProcesses ?? Enumerable.Empty<ImprovementProcess>())
                .Where(x => x.Process != null)
                .Select(x => x.Process!.Name)
                .ToList();
            var services = (i.ImprovementServices ?? Enumerable.Empty<Models.Improvement.ImprovementService>())
                .Where(x => x.Service != null)
                .Select(x => x.Service!.Name)
                .ToList();
            var costDelta = (i.ActualCostSavings ?? 0) - (i.EstimatedCostSavings ?? 0);

            var row = new[]
            {
                i.Code ?? "",
                i.TitleEn ?? "",
                i.TitleAr ?? "",
                i.Status.ToString(),
                i.Quadrant.ToString(),
                i.Horizon?.ToString() ?? "",
                i.InnovationType?.ToString() ?? "",
                i.TotalPrioritizationScore?.ToString("N2") ?? "",
                i.OwningUnit?.Name ?? "",
                string.Join("; ", processes),
                string.Join("; ", services),
                i.EstimatedCostSavings?.ToString("N2") ?? "",
                i.ActualCostSavings?.ToString("N2") ?? "",
                costDelta.ToString("N2"),
                i.EstimatedTimeSavings?.ToString("N2") ?? "",
                i.ActualTimeSavings?.ToString("N2") ?? "",
                i.TargetDate?.ToString("yyyy-MM-dd") ?? "",
                i.CompletedDate?.ToString("yyyy-MM-dd") ?? "",
                closure?.LessonsLearnedEn ?? "",
                closure?.LessonsLearnedAr ?? "",
                closure?.SignedOffByName ?? "",
                closure?.ClosedByName ?? ""
            };
            sb.AppendLine(string.Join(",", row.Select(CsvQuote)));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var filename = $"DGEP_Excellence_Export_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", filename);
    }

    /// <summary>
    /// CSV-escape a single cell value. Wraps in double quotes and
    /// doubles any embedded quotes, so values containing commas,
    /// newlines, or quotes round-trip correctly.
    /// </summary>
    private static string CsvQuote(string? value)
    {
        if (value == null) return "\"\"";
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stage-gate reviews — Pass D
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// File a new stage-gate review. Called from the Reviews tab on the
    /// Details page. Allowed for any initiative that is not in a
    /// terminal state.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> AddReview(
        string id,
        string healthStatus,
        string? notesEn,
        string? notesAr,
        int? progressPercentage,
        DateTime? nextReviewDate)
    {
        var improvement = await FindInitiativeAsync(id);
        if (improvement == null) return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement)) return NotFound();

        if (ImprovementStatusMachine.Terminal.Contains(improvement.Status))
        {
            TempData["Error"] = _localizer["Error_CannotReviewClosedInitiative"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        var allowedHealth = new[] { "Green", "Amber", "Red" };
        if (!allowedHealth.Contains(healthStatus)) healthStatus = "Green";

        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        var review = new ImprovementReview
        {
            Id = Guid.NewGuid().ToString(),
            ImprovementId = id,
            ReviewDate = DateTime.UtcNow,
            HealthStatus = healthStatus,
            NotesEn = notesEn,
            NotesAr = notesAr,
            ProgressPercentageSnapshot = progressPercentage,
            NextReviewDate = nextReviewDate ?? DateTime.UtcNow.AddMonths(3),
            ReviewedById = userId,
            ReviewedByName = userName,
            CreatedAt = DateTime.UtcNow
        };
        _context.ImprovementReviews.Add(review);

        // If the reviewer snapped a progress %, roll it onto the parent.
        if (progressPercentage.HasValue)
        {
            improvement.ProgressPercentage = Math.Clamp(progressPercentage.Value, 0, 100);
        }
        improvement.UpdatedAt = DateTime.UtcNow;
        improvement.UpdatedById = userName;

        await _context.SaveChangesAsync();

        // If the review is Amber or Red, notify the owner (if they're not
        // the reviewer) so they see the flag immediately.
        if (healthStatus != "Green" &&
            !string.IsNullOrWhiteSpace(improvement.OwnerId) &&
            int.TryParse(improvement.OwnerId, out var ownerId) &&
            ownerId != userId)
        {
            try
            {
                await _notificationService.SendAsync(ownerId,
                    $"Stage-gate review: {healthStatus}",
                    $"مراجعة المرحلة: {healthStatus}",
                    $"'{improvement.TitleEn}' was reviewed by {userName} and flagged {healthStatus}. See Details → Reviews.",
                    $"تمت مراجعة '{improvement.TitleAr}' بواسطة {userName} وتم تصنيفها {healthStatus}. راجع التفاصيل → المراجعات.",
                    healthStatus == "Red" ? "Error" : "Warning",
                    improvement.Id,
                    "Improvement",
                    $"/Improvements/Details/{improvement.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send stage-gate review notification");
            }
        }

        TempData["Success"] = _localizer["Success_ReviewRecorded"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    // ═══════════════════════════════════════════════════════════════════
    // Closure — Pass C
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Formal closure: persist a <see cref="ImprovementClosureReport"/>,
    /// stamp ActualCostSavings / ActualTimeSavings / CompletedDate on the
    /// initiative, and transition status → Completed. Called from the
    /// "Close Initiative" modal on the Details page.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> Close(
        string id,
        decimal? actualCostSavings,
        decimal? actualTimeSavings,
        string? lessonsLearnedEn,
        string? lessonsLearnedAr,
        string? closingComments,
        bool signOffNow = false)
    {
        var improvement = await FindInitiativeAsync(id);
        if (improvement == null) return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement)) return NotFound();

        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        // Upsert the closure report — if the user re-opens and re-closes,
        // we want to edit the existing row rather than fail.
        var report = await _context.ImprovementClosureReports
            .FirstOrDefaultAsync(r => r.ImprovementId == id);
        if (report == null)
        {
            report = new ImprovementClosureReport
            {
                Id = Guid.NewGuid().ToString(),
                ImprovementId = id,
                ClosedAt = DateTime.UtcNow
            };
            _context.ImprovementClosureReports.Add(report);
        }
        report.LessonsLearnedEn = lessonsLearnedEn;
        report.LessonsLearnedAr = lessonsLearnedAr;
        report.ClosingComments = closingComments;
        report.ClosedById = userId;
        report.ClosedByName = userName;
        if (signOffNow)
        {
            report.SignedOffById = userId;
            report.SignedOffByName = userName;
            report.SignedOffAt = DateTime.UtcNow;
        }

        // Audit #8: block the transition to Closed unless sponsor sign-off
        // is on file. A draft closure report (no sign-off yet) parks the
        // initiative at Completed; a subsequent sign-off via this same
        // endpoint flips it to Closed. Prevents the historical "closed-but-
        // never-accountable" silent path that DGEP §3.2 explicitly forbids.
        var hasSignOff = report.SignedOffById.HasValue
                         && !string.IsNullOrWhiteSpace(report.SignedOffByName)
                         && report.SignedOffAt.HasValue;

        if (improvement.Status == ImprovementStatus.InProgress)
        {
            ImprovementStatusMachine.EnsureTransition(improvement.Status, ImprovementStatus.Completed);
            improvement.Status = ImprovementStatus.Completed;
        }
        if (hasSignOff && improvement.Status == ImprovementStatus.Completed)
        {
            ImprovementStatusMachine.EnsureTransition(improvement.Status, ImprovementStatus.Closed);
            improvement.Status = ImprovementStatus.Closed;
        }

        // Roll up onto the initiative
        improvement.ActualCostSavings = actualCostSavings ?? improvement.ActualCostSavings;
        improvement.ActualTimeSavings = actualTimeSavings ?? improvement.ActualTimeSavings;
        improvement.CompletedDate = DateTime.UtcNow;
        improvement.ProgressPercentage = 100;
        improvement.UpdatedAt = DateTime.UtcNow;
        improvement.UpdatedById = userName;

        await _context.SaveChangesAsync();

        // Notify the process owner (if one is assigned and isn't the closer)
        // so they know the initiative is awaiting sign-off / archive.
        if (!string.IsNullOrWhiteSpace(improvement.OwnerId) &&
            int.TryParse(improvement.OwnerId, out var ownerId) &&
            ownerId != userId)
        {
            try
            {
                await _notificationService.SendAsync(ownerId,
                    "Initiative closed",
                    "تم إغلاق المبادرة",
                    $"'{improvement.TitleEn}' has been closed by {userName}. Review the closure report on the Details page.",
                    $"تم إغلاق '{improvement.TitleAr}' بواسطة {userName}. راجع تقرير الإغلاق في صفحة التفاصيل.",
                    "Success",
                    improvement.Id,
                    "Improvement",
                    $"/Improvements/Details/{improvement.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send closure notification");
            }
        }

        TempData["Success"] = improvement.Status == ImprovementStatus.Closed
            ? (_localizer["Success_ImprovementClosed"].Value)
            : (_localizer["Success_ImprovementClosureUpdated"].Value);
        return RedirectToAction(nameof(Details), new { id });
    }

    // ═══════════════════════════════════════════════════════════════════
    // Measurement readings — time-series collection during execution
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Record a new reading for a measurement. Called from the "Record
    /// Reading" modal on the Measurements tab. Period is computed
    /// server-side from the measurement's cadence + the current date.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> RecordReading(string measurementId, string? value, string? notes = null)
    {
        var measurement = await _context.ImprovementMeasurements
            .FirstOrDefaultAsync(m => m.Id == measurementId);
        if (measurement == null) return NotFound();

        // F-006: record-level scope (IDOR) via the measurement's owning initiative.
        var owner = await FindInitiativeAsync(measurement.ImprovementId);
        if (owner == null || !(await _scopingService.GetScopeAsync(User)).CanAccess(owner))
            return NotFound();

        // Bug fix: previously the binder accepted `decimal value` and bound
        // empty/missing form fields to 0 silently, which then OVERWROTE any
        // existing reading for the current period via the upsert. Now we
        // parse the raw string and reject empty/non-numeric input outright.
        var isAr = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value)
            || !decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedValue))
        {
            TempData["Error"] = isAr
                ? "يرجى إدخال قيمة رقمية للقراءة."
                : "Please enter a numeric reading value.";
            return RedirectToAction(nameof(Details), new { id = measurement.ImprovementId });
        }

        var now = DateTime.UtcNow;
        var period = _measurementCollection.PeriodFor(measurement, now);

        try
        {
            await _measurementCollection.RecordReadingAsync(
                measurementId,
                period.PeriodLabel,
                period.PeriodStart,
                parsedValue,
                notes,
                GetCurrentUserId());
            TempData["Success"] = isAr
                ? $"تم تسجيل القراءة {parsedValue} {measurement.UnitOfMeasure} للفترة {period.PeriodLabel}."
                : $"Reading {parsedValue} {measurement.UnitOfMeasure} recorded for {period.PeriodLabel}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record reading for measurement {MeasurementId}", measurementId);
            TempData["Error"] = (isAr ? "فشل تسجيل القراءة: " : "Failed to record reading: ") + ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = measurement.ImprovementId });
    }

    /// <summary>
    /// Personal inbox — every measurement on every initiative the current
    /// user owns where the current period has no reading yet. The
    /// Details → Measurements → green-+ flow is 3 clicks per reading;
    /// this page is 1 click (or none if linked from the notification bell).
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Improvement.View)]
    public async Task<IActionResult> MyPendingReadings()
    {
        var userId = GetCurrentUserId();
        var due = await _measurementCollection.GetDueReadingsAsync(userId);
        return View(due);
    }

    /// <summary>
    /// Batch save from the MyPendingReadings inbox. Form is a list of
    /// measurementId + value pairs. Each row goes through the same
    /// RecordReadingAsync path as the single-record modal so cadence,
    /// upsert, and validation behave identically. Empty values are
    /// silently skipped (the user can record any subset).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> SaveBatchReadings([FromForm] string[] measurementId, [FromForm] string[] value)
    {
        if (measurementId == null || value == null || measurementId.Length != value.Length)
        {
            TempData["Error"] = _localizer["Error_InvalidBatchPayload"].Value;
            return RedirectToAction(nameof(MyPendingReadings));
        }
        var isAr = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        int saved = 0, skipped = 0, failed = 0;
        var now = DateTime.UtcNow;
        var userId = GetCurrentUserId();
        // F-006: resolve scope once for the per-row IDOR guard below.
        var scope = await _scopingService.GetScopeAsync(User);
        for (int i = 0; i < measurementId.Length; i++)
        {
            var raw = value[i];
            if (string.IsNullOrWhiteSpace(raw)) { skipped++; continue; }
            if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            { failed++; continue; }
            try
            {
                var measurement = await _context.ImprovementMeasurements.FirstOrDefaultAsync(m => m.Id == measurementId[i]);
                if (measurement == null) { failed++; continue; }
                // F-006: record-level scope (IDOR) via the measurement's owning initiative.
                var owner = await FindInitiativeAsync(measurement.ImprovementId);
                if (owner == null || !scope.CanAccess(owner)) { failed++; continue; }
                var period = _measurementCollection.PeriodFor(measurement, now);
                await _measurementCollection.RecordReadingAsync(measurementId[i], period.PeriodLabel, period.PeriodStart, parsed, null, userId);
                saved++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch reading save failed for measurement {Id}", measurementId[i]);
                failed++;
            }
        }
        TempData["Success"] = isAr
            ? $"تم حفظ {saved} قراءة. تم تخطي {skipped}."
            : $"Saved {saved} readings. Skipped {skipped}.";
        if (failed > 0)
        {
            TempData["Error"] = isAr
                ? $"فشل حفظ {failed} قراءة."
                : $"{failed} reading(s) failed to save.";
        }
        return RedirectToAction(nameof(MyPendingReadings));
    }

    /// <summary>
    /// JSON endpoint — returns the time series for one measurement so
    /// the Measurements tab can plot a trend chart without a page reload.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReadings(string measurementId)
    {
        // F-006: record-level scope (IDOR) via the measurement's owning initiative.
        var measurement = await _context.ImprovementMeasurements
            .FirstOrDefaultAsync(m => m.Id == measurementId);
        if (measurement == null) return NotFound();
        var owner = await FindInitiativeAsync(measurement.ImprovementId);
        if (owner == null || !(await _scopingService.GetScopeAsync(User)).CanAccess(owner))
            return NotFound();

        var trend = await _measurementCollection.GetTrendAsync(measurementId);
        return Json(trend.Select(r => new
        {
            periodLabel = r.PeriodLabel,
            periodStart = r.PeriodStart.ToString("yyyy-MM-dd"),
            value = r.Value,
            notes = r.Notes,
            enteredAt = r.EnteredAt?.ToString("yyyy-MM-dd HH:mm")
        }));
    }

    /// <summary>
    /// Execution transitions — Start / Pause / Resume / Cancel / Archive.
    /// These don't go through the approval workflow; they mutate status
    /// directly and notify interested parties.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> Transition(string id, string target, string? comments = null)
    {
        var improvement = await FindInitiativeAsync(id);
        if (improvement == null) return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement)) return NotFound();

        // F-001 / FLOW-011: Transition must not be an approval back-door. Approving
        // or rejecting is a governance decision that has to run through the approval
        // workflow (Submit → assigned approver → Approve/Reject) so the
        // WorkflowInstance, approver assignment, and self-approval / dual-level
        // guards apply. Block those targets here even though the FSM permits them —
        // this action is gated only on Improvement.Edit, which Editors hold without
        // Improvement.Approve.
        if (Enum.TryParse<ImprovementStatus>(target, ignoreCase: true, out var requestedStatus)
            && requestedStatus is ImprovementStatus.Approved or ImprovementStatus.Rejected)
        {
            TempData["Error"] = $"'{requestedStatus}' can only be set through the approval workflow, not this action. Use Submit, then approve/reject from the approvals inbox.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!Enum.TryParse<ImprovementStatus>(target, ignoreCase: true, out var targetStatus)
            || !ImprovementStatusMachine.CanTransition(improvement.Status, targetStatus))
        {
            TempData["Error"] = $"Cannot transition from '{improvement.Status}' to '{target}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var previousStatus = improvement.Status;
        improvement.Status = targetStatus;
        improvement.UpdatedAt = DateTime.UtcNow;
        improvement.UpdatedById = GetCurrentUserName();

        // Mark completion date if closing
        if (targetStatus == ImprovementStatus.Completed && !improvement.CompletedDate.HasValue)
            improvement.CompletedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify the owner of any state change they didn't initiate themselves
        if (!string.IsNullOrWhiteSpace(improvement.OwnerId) &&
            int.TryParse(improvement.OwnerId, out var ownerId) &&
            ownerId != GetCurrentUserId())
        {
            await _notificationService.SendAsync(ownerId,
                $"Initiative status: {target}",
                $"حالة المبادرة: {target}",
                $"'{improvement.TitleEn}' transitioned from {previousStatus} to {target}." + (string.IsNullOrWhiteSpace(comments) ? "" : $" Note: {comments}"),
                $"انتقلت '{improvement.TitleAr}' من {previousStatus} إلى {target}." + (string.IsNullOrWhiteSpace(comments) ? "" : $" ملاحظة: {comments}"),
                "Info",
                improvement.Id,
                "Improvement",
                $"/Improvements/Details/{improvement.Id}");
        }

        TempData["Success"] = _localizer["Success_ImprovementUpdated"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
	    /// Improvement wizard (4-step connected flow)
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Improvement.Create)]
    public async Task<IActionResult> Wizard()
    {
        await PopulateDropdowns();
        var model = new ImprovementInitiative();

        // Pre-populate from AI optimizer suggestions (query string params)
        if (Request.Query.TryGetValue("processId", out var pid) && !string.IsNullOrWhiteSpace(pid))
            model.ProcessId = pid;
        if (Request.Query.TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title))
        {
            model.TitleEn = title;
            model.TitleAr = title;
        }
        if (Request.Query.TryGetValue("description", out var desc) && !string.IsNullOrWhiteSpace(desc))
        {
            model.DescriptionEn = desc;
            model.DescriptionAr = desc;
        }
        if (Request.Query.TryGetValue("estimatedCostSavings", out var cost) && decimal.TryParse(cost, out var costVal))
            model.EstimatedCostSavings = costVal;

        // Resume from a saved draft — the view's JS hydrates the form from the
        // draft payload via /Improvements/LoadDraft. We just thread the draftId
        // through to the view so the client knows which one to fetch.
        if (Request.Query.TryGetValue("draftId", out var draftId) && !string.IsNullOrWhiteSpace(draftId))
        {
            ViewBag.ResumeDraftId = draftId.ToString();
        }

        return View(model);
    }

    // ─── Drafts ─────────────────────────────────────────────────────────
    // Per-user, in-progress wizard snapshots. Lets users abandon mid-flow
    // and resume later without losing the click-through they've invested.
    // Every action filters on User.Identity.Name so a user can't see or
    // touch another user's drafts.

    public class SaveDraftRequest
    {
        public string? Id { get; set; }              // null = new draft
        public string? Title { get; set; }           // best-effort label
        public string PayloadJson { get; set; } = "";
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Improvement.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft([FromBody] SaveDraftRequest req)
    {
        var owner = User.Identity?.Name ?? "";
        if (string.IsNullOrEmpty(owner))
            return Unauthorized();
        if (req == null || string.IsNullOrWhiteSpace(req.PayloadJson))
            return BadRequest(new { error = "Empty payload." });

        ImprovementDraft draft;
        if (!string.IsNullOrWhiteSpace(req.Id))
        {
            draft = await _context.ImprovementDrafts
                .FirstOrDefaultAsync(d => d.Id == req.Id && d.OwnerId == owner)
                ?? throw new InvalidOperationException("Draft not found.");
            draft.PayloadJson = req.PayloadJson;
            draft.Title = string.IsNullOrWhiteSpace(req.Title)
                ? draft.Title
                : req.Title.Trim();
            draft.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            draft = new ImprovementDraft
            {
                Id = Guid.NewGuid().ToString(),
                OwnerId = owner,
                Title = string.IsNullOrWhiteSpace(req.Title)
                    ? $"Untitled draft — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC"
                    : req.Title.Trim(),
                PayloadJson = req.PayloadJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ImprovementDrafts.Add(draft);
        }
        await _context.SaveChangesAsync();
        return Json(new { id = draft.Id, title = draft.Title, updatedAt = draft.UpdatedAt });
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.Module.Improvement.Create)]
    public async Task<IActionResult> LoadDraft(string id)
    {
        var owner = User.Identity?.Name ?? "";
        var draft = await _context.ImprovementDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == owner);
        if (draft == null) return NotFound();
        return Json(new { id = draft.Id, title = draft.Title, payloadJson = draft.PayloadJson, updatedAt = draft.UpdatedAt });
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Improvement.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDraft(string id)
    {
        var owner = User.Identity?.Name ?? "";
        var draft = await _context.ImprovementDrafts
            .FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == owner);
        if (draft != null)
        {
            _context.ImprovementDrafts.Remove(draft);
            await _context.SaveChangesAsync();
        }
        return Json(new { success = true });
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.Module.Improvement.Create)]
    public async Task<IActionResult> MyDrafts()
    {
        var owner = User.Identity?.Name ?? "";
        var rows = await _context.ImprovementDrafts
            .AsNoTracking()
            .Where(d => d.OwnerId == owner)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new { id = d.Id, title = d.Title, updatedAt = d.UpdatedAt })
            .ToListAsync();
        return Json(rows);
    }

    /// <summary>Bulk delete every draft owned by the current user.
    /// Used by the "Clear all" affordance on the Improvements/Index drafts banner.</summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Improvement.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearMyDrafts()
    {
        var owner = User.Identity?.Name ?? "";
        if (string.IsNullOrEmpty(owner)) return Unauthorized();
        var drafts = await _context.ImprovementDrafts.Where(d => d.OwnerId == owner).ToListAsync();
        if (drafts.Count > 0)
        {
            _context.ImprovementDrafts.RemoveRange(drafts);
            await _context.SaveChangesAsync();
        }
        return Json(new { cleared = drafts.Count });
    }

    /// <summary>
    /// Create improvement from wizard with measurements
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Improvement.Create)]
    [ValidateAntiForgeryToken]
	    public async Task<IActionResult> CreateFromWizard(
	        [FromForm] ImprovementInitiative improvement,
	        [FromForm] string[]? SelectedProcessIds,
	        [FromForm] string[]? SelectedServiceIds,
        [FromForm] string? MeasurementTypes, [FromForm] string? MeasurementNames,
        [FromForm] string? MeasurementUnits, [FromForm] string? MeasurementTargets,
        [FromForm] string? MeasurementAsIs, [FromForm] string? MeasurementToBe,
        [FromForm] string? MeasurementWeights, [FromForm] string? MeasurementNamesAr,
        [FromForm] string? MeasurementAppliesTo, [FromForm] string? MeasurementPeriods,
        [FromForm] string? MeasurementMethods, [FromForm] string? MeasurementBpmn,
        [FromForm] string? MeasurementPriorities,
        [FromForm] string? MeasurementDirections,
        [FromForm] string? MeasurementKpiDefinitionIds,
        [FromForm] string? TeamUserIds, [FromForm] string? TeamRoles, [FromForm] string? TeamNotes,
        [FromForm] string? ActionNames, [FromForm] string? ActionNamesAr,
        [FromForm] string? ActionAssignees, [FromForm] string? ActionDueDates,
        [FromForm] string? DraftId)
    {
        var mp = new WizardMeasurementParams
        {
            MeasurementTypes = MeasurementTypes, MeasurementNames = MeasurementNames,
            MeasurementNamesAr = MeasurementNamesAr, MeasurementUnits = MeasurementUnits,
            MeasurementTargets = MeasurementTargets, MeasurementAsIs = MeasurementAsIs,
            MeasurementToBe = MeasurementToBe, MeasurementWeights = MeasurementWeights,
            MeasurementAppliesTo = MeasurementAppliesTo, MeasurementPeriods = MeasurementPeriods,
            MeasurementMethods = MeasurementMethods, MeasurementBpmn = MeasurementBpmn,
            MeasurementPriorities = MeasurementPriorities,
            MeasurementDirections = MeasurementDirections,
            MeasurementKpiDefinitionIds = MeasurementKpiDefinitionIds
        };

        // Mirror Title → Name. The Improvement entity has its own TitleEn /
        // TitleAr [Required] fields (the wizard form posts these), but it
        // ALSO inherits NameEn / NameAr [Required] + [MinLength(3)] from
        // BilingualEntity. The wizard never collects Name separately, so
        // the inherited Required failed silently — ModelState invalid with
        // property-keyed errors that asp-validation-summary="ModelOnly"
        // doesn't surface, so the user saw a 200 with no error and no
        // saved record. For Improvements, Name IS the Title; copy across
        // and clear the bound binding errors before the IsValid check.
        if (!string.IsNullOrWhiteSpace(improvement.TitleEn))
            improvement.NameEn = improvement.TitleEn;
        if (!string.IsNullOrWhiteSpace(improvement.TitleAr))
            improvement.NameAr = improvement.TitleAr;
        ModelState.Remove(nameof(ImprovementInitiative.NameEn));
        ModelState.Remove(nameof(ImprovementInitiative.NameAr));

        var validation = _improvementService.ValidateWizardInput(improvement, SelectedProcessIds, SelectedServiceIds, mp);

        foreach (var error in validation.Errors)
            ModelState.AddModelError(string.Empty, error);

        if (ModelState.IsValid)
        {
            // Audit #6 + #17: stamp Code from HierarchicalCodeService and apply
            // configured Quadrant cut-offs before persistence. SaveWizard...
            // does its own SaveChanges, so AllocateWithRetry retries the whole
            // wizard payload on a unique-constraint loss.
            var (impactCutoff, effortCutoff) = await GetQuadrantCutoffsAsync();
            improvement.CalculateQuadrant(impactCutoff, effortCutoff);

            await _codeSvc.AllocateWithRetryAsync(async () =>
            {
                improvement.Code = await _codeSvc.NextInitiativeCodeAsync();
                await _improvementService.SaveWizardImprovementAsync(
                    improvement, validation.NormalizedProcessIds, validation.NormalizedServiceIds,
                    mp, User.Identity?.Name);
                return 0;
            });

            // Save team members
            var userIds = (TeamUserIds ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
            var roles = (TeamRoles ?? "").Split('|', StringSplitOptions.None);
            var notes = (TeamNotes ?? "").Split('|', StringSplitOptions.None);
            for (int i = 0; i < userIds.Length; i++)
            {
                // Audit #14: TeamMember.UserId is now an enforced int FK; skip
                // any row whose UserId can't be parsed (defends against legacy
                // wizard payloads that submitted non-numeric tokens).
                if (Enum.TryParse<Models.Enums.ImprovementLifecycleRole>(i < roles.Length ? roles[i] : "", out var role)
                    && int.TryParse(userIds[i], out var teamUserId))
                {
                    _context.Set<Models.Improvement.ImprovementTeamMember>().Add(new Models.Improvement.ImprovementTeamMember
                    {
                        Id = Guid.NewGuid().ToString(),
                        ImprovementId = improvement.Id,
                        UserId = teamUserId,
                        Role = role,
                        Notes = i < notes.Length ? notes[i] : null,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // Save action plans
            var actionNamesArr = (ActionNames ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
            var actionNamesArArr = (ActionNamesAr ?? "").Split('|', StringSplitOptions.None);
            var actionAssignees = (ActionAssignees ?? "").Split('|', StringSplitOptions.None);
            var actionDueDates = (ActionDueDates ?? "").Split('|', StringSplitOptions.None);
            for (int i = 0; i < actionNamesArr.Length; i++)
            {
                _context.Set<Models.Improvement.ImprovementAction>().Add(new Models.Improvement.ImprovementAction
                {
                    Id = Guid.NewGuid().ToString(),
                    Code = $"ACT-{i + 1:D3}",
                    ImprovementId = improvement.Id,
                    NameEn = actionNamesArr[i],
                    NameAr = i < actionNamesArArr.Length ? actionNamesArArr[i] : "",
                    AssignedToId = i < actionAssignees.Length && !string.IsNullOrWhiteSpace(actionAssignees[i]) ? actionAssignees[i] : null,
                    DueDate = i < actionDueDates.Length && DateTime.TryParse(actionDueDates[i], out var dd) ? dd : null,
                    DisplayOrder = i,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Drop the resumed draft now that it's been turned into a real
            // initiative — keeping it would only confuse the user's drafts list.
            if (!string.IsNullOrWhiteSpace(DraftId))
            {
                var owner = User.Identity?.Name ?? "";
                var draft = await _context.ImprovementDrafts
                    .FirstOrDefaultAsync(d => d.Id == DraftId && d.OwnerId == owner);
                if (draft != null)
                {
                    _context.ImprovementDrafts.Remove(draft);
                    await _context.SaveChangesAsync();
                }
            }

            await NotifyOwnerOfCreationAsync(improvement, GetCurrentUserId());

            TempData["Success"] = _localizer["Success_ImprovementCreatedViaWizard"].Value;
            return RedirectToAction(nameof(Details), new { id = improvement.Id });
        }

        await PopulateDropdowns();
        return View("Wizard", improvement);
    }

    /// <summary>
    /// Improvement Roadmap — auto-generated from quadrant analysis with intelligent prioritization
    /// Uses: Quadrant (primary), TotalPrioritizationScore (ranking), Horizon (timeline)
    ///
    /// Plan X pilot: this is the first action to migrate from the legacy
    /// <c>CanView</c> policy to a granular <c>Module.Action</c> policy.
    /// Proves the matrix-driven authorization works end-to-end.
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Improvement.View)]
    public async Task<IActionResult> Roadmap()
    {
        var scope = await _scopingService.GetScopeAsync(User);
        var improvements = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted)
            .ApplyOwningUnitScope(scope)
	            .Include(i => i.ImprovementProcesses)
	                .ThenInclude(ip => ip.Process)
	            .Include(i => i.ImprovementServices)
	                .ThenInclude(isv => isv.Service)
            .Include(i => i.Process)
            .Include(i => i.Service)
            .Include(i => i.OwningUnit)
            .Include(i => i.Measurements.Where(m => !m.IsDeleted))
            .ToListAsync();

        var model = _improvementService.GenerateRoadmap(improvements);
        return View(model);
    }

    /// <summary>
    /// API: Get measurement comparison data for a single improvement (As-Is vs To-Be vs Target)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ComparisonData(string id)
    {
        // F-006: record-level scope (IDOR) on the owning initiative.
        var improvement = await FindInitiativeAsync(id);
        if (improvement == null) return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement))
            return NotFound();

        var measurements = await _context.ImprovementMeasurements
            .Where(m => m.ImprovementId == id && !m.IsDeleted && m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync();

        var data = measurements.Select(m => new
        {
            name = m.NameEn,
            unit = m.UnitOfMeasure,
            type = m.MeasurementType.ToString(),
            asIs = m.AsIsValue,
            toBe = m.ToBeValue,
            target = m.TargetValue,
            improvementPct = m.GetImprovementPercentage(),
            achievementPct = m.GetTargetAchievementPercentage()
        });

        return Json(data);
    }

    /// <summary>
    /// API: Add a measurement to an existing improvement
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> AddMeasurement([FromBody] ImprovementMeasurement measurement)
    {
        try
        {
            var improvement = await _context.ImprovementInitiatives.FindAsync(measurement.ImprovementId);
            if (improvement == null || improvement.IsDeleted)
                return Json(new { success = false, error = "Improvement not found" });

            // F-006: record-level scope (IDOR) on the owning initiative.
            if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement))
                return Json(new { success = false, error = "Improvement not found" });

            // Server-side defense in depth — the modal already validates,
            // but a direct POST (CSRF token in hand) could otherwise smuggle
            // garbage rows past the form. Mirrors the wizard's required
            // checks so dashboards never have to show a "—" row.
            var validation = ValidateMeasurementShape(measurement);
            if (validation != null) return Json(new { success = false, error = validation });

            measurement.Id = Guid.NewGuid().ToString();
            measurement.CreatedAt = DateTime.UtcNow;
            measurement.UpdatedAt = DateTime.UtcNow;
            // Default the cadence so the reminder service can ping the owner.
            if (string.IsNullOrWhiteSpace(measurement.MeasuringPeriod))
                measurement.MeasuringPeriod = "Monthly";
            _context.ImprovementMeasurements.Add(measurement);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = measurement.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding measurement");
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// API: Update a measurement
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> UpdateMeasurement([FromBody] ImprovementMeasurement measurement)
    {
        try
        {
            var existing = await _context.ImprovementMeasurements.FindAsync(measurement.Id);
            if (existing == null || existing.IsDeleted)
                return Json(new { success = false, error = "Measurement not found" });

            // F-006: record-level scope (IDOR) via the measurement's owning initiative.
            var owner = await FindInitiativeAsync(existing.ImprovementId);
            if (owner == null || !(await _scopingService.GetScopeAsync(User)).CanAccess(owner))
                return Json(new { success = false, error = "Measurement not found" });

            // Same shape validation as Add — keeps the API symmetric so a
            // client can't update an existing row into a state that Add
            // would have rejected.
            var validation = ValidateMeasurementShape(measurement);
            if (validation != null) return Json(new { success = false, error = validation });

            existing.NameEn = measurement.NameEn;
            existing.NameAr = measurement.NameAr;
            existing.MeasurementType = measurement.MeasurementType;
            existing.UnitOfMeasure = measurement.UnitOfMeasure;
            existing.TargetValue = measurement.TargetValue;
            existing.AsIsValue = measurement.AsIsValue;
            existing.ToBeValue = measurement.ToBeValue;
            existing.Weight = measurement.Weight;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating measurement");
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Shared shape validation for AddMeasurement / UpdateMeasurement.
    /// Returns the first violation as a user-facing string, or null if OK.
    ///
    /// Required:
    ///   * At least one of NameEn / NameAr (the measurement is shown
    ///     bilingually but at least one name must exist for tables to
    ///     render something useful).
    ///   * UnitOfMeasure (otherwise the "Now → Target" column reads
    ///     dimensionless and dashboards have no axis label).
    ///
    /// Sanity:
    ///   * If both AsIsValue and TargetValue are set, they must not be
    ///     equal (no journey to measure → achievement % is undefined).
    ///   * Direction-respecting target check: HigherBetter expects
    ///     Target &gt; Baseline; LowerBetter expects Target &lt; Baseline.
    ///     Mismatch likely means the user mis-filled direction or values.
    /// </summary>
    private static string? ValidateMeasurementShape(ImprovementMeasurement m)
    {
        // Read CurrentUICulture inside the validator so callers don't have to
        // pass an isRtl flag — same pattern the views use everywhere.
        var isAr = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(m.NameEn) && string.IsNullOrWhiteSpace(m.NameAr))
            return isAr
                ? "اسم المقياس مطلوب (إنجليزي أو عربي)."
                : "Measurement name is required (English or Arabic).";
        if (string.IsNullOrWhiteSpace(m.UnitOfMeasure))
            return isAr
                ? "وحدة القياس مطلوبة (مثل: %، دقائق، درهم)."
                : "Unit of measure is required (e.g. %, minutes, AED).";
        if (m.AsIsValue.HasValue && m.TargetValue.HasValue)
        {
            if (m.AsIsValue.Value == m.TargetValue.Value)
                return isAr
                    ? "القيمة الحالية والهدف لا يمكن أن يتساويا — لا يوجد مسار للقياس."
                    : "Baseline and target cannot be the same value — there's no journey to measure.";
            if (m.Direction == MeasurementDirection.HigherBetter && m.TargetValue.Value < m.AsIsValue.Value)
                return isAr
                    ? "في اتجاه \"أعلى أفضل\"، يجب أن يكون الهدف أكبر من القيمة الحالية."
                    : "For 'Higher is better' direction, the target must be greater than the baseline.";
            if (m.Direction == MeasurementDirection.LowerBetter && m.TargetValue.Value > m.AsIsValue.Value)
                return isAr
                    ? "في اتجاه \"أقل أفضل\"، يجب أن يكون الهدف أقل من القيمة الحالية."
                    : "For 'Lower is better' direction, the target must be less than the baseline.";
        }
        return null;
    }

    /// <summary>
    /// API: Delete a measurement
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> DeleteMeasurement([FromBody] DeleteMeasurementRequest request)
    {
        try
        {
            var measurement = await _context.ImprovementMeasurements.FindAsync(request.Id);
            if (measurement == null)
                return Json(new { success = false, error = "Measurement not found" });

            // F-006: record-level scope (IDOR) via the measurement's owning initiative.
            var owner = await FindInitiativeAsync(measurement.ImprovementId);
            if (owner == null || !(await _scopingService.GetScopeAsync(User)).CanAccess(owner))
                return Json(new { success = false, error = "Measurement not found" });

            measurement.IsDeleted = true;
            measurement.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting measurement");
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Add a team member to an improvement initiative
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> AddTeamMember(string improvementId, string userId, ImprovementLifecycleRole role, string? notes)
    {
        // Audit #14: TeamMember.UserId is an int FK; reject non-numeric early
        // with a clean 400 instead of silently accepting bad data.
        if (!int.TryParse(userId, out var teamUserId))
            return BadRequest(new { success = false, message = "UserId must be a numeric user identifier." });

        // Check if improvement exists
        var improvement = await _context.ImprovementInitiatives
            .FirstOrDefaultAsync(i => i.Id == improvementId && !i.IsDeleted);

        if (improvement == null)
            return NotFound();

        // F-006: record-level scope (IDOR) on the owning initiative.
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement))
            return NotFound();

        // Check if user is already assigned
        var existingMember = await _context.ImprovementTeamMembers
            .FirstOrDefaultAsync(tm => tm.ImprovementId == improvementId && tm.UserId == teamUserId && tm.IsActive);

        if (existingMember != null)
            return Json(new { success = false, message = _localizer["Error_TeamMemberExists"].Value });

        // Add team member
        var teamMember = new ImprovementTeamMember
        {
            ImprovementId = improvementId,
            UserId = teamUserId,
            Role = role,
            Notes = notes,
            IsActive = true
        };

        _context.ImprovementTeamMembers.Add(teamMember);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = _localizer["Success_TeamMemberAdded"].Value });
    }

    /// <summary>
    /// Remove a team member from an improvement initiative
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> RemoveTeamMember(string teamMemberId)
    {
        var teamMember = await _context.ImprovementTeamMembers
            .FirstOrDefaultAsync(tm => tm.Id == teamMemberId);

        if (teamMember == null)
            return NotFound();

        // F-006: record-level scope (IDOR) via the team member's owning initiative.
        var owner = await FindInitiativeAsync(teamMember.ImprovementId);
        if (owner == null || !(await _scopingService.GetScopeAsync(User)).CanAccess(owner))
            return NotFound();

        teamMember.IsActive = false;
        teamMember.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = _localizer["Success_TeamMemberRemoved"].Value });
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Processes = new SelectList(
            await _context.Processes.Where(p => !p.IsDeleted).OrderBy(p => p.Code).ToListAsync(),
            "Id", "Name");
        ViewBag.Services = new SelectList(
            await _context.Services.Where(s => !s.IsDeleted).ToListAsync(),
            "Id", "Name");
        ViewBag.OrganizationUnits = new SelectList(
            await _context.OrganizationUnits.Where(u => !u.IsDeleted && u.IsActive).ToListAsync(),
            "Id", "Name");
        // Owner picker: render as a searchable combobox. We keep the stock
        // SelectList for back-compat with any view still using a native
        // <select asp-items>, and add a parallel list that exposes the
        // email for client-side filtering (by partial name OR email).
        var users = await _context.Users
            .Select(u => new { u.UserId, u.EmployeeName, u.EmailAddress })
            .ToListAsync();
        ViewBag.Users = new SelectList(users, "UserId", "EmployeeName");
        ViewBag.UserCombo = users
            .Select(u => new { id = u.UserId, name = u.EmployeeName ?? string.Empty, email = u.EmailAddress ?? string.Empty })
            .ToList();
    }

    #region Improvement-Risk Relationship Management

    // ---- Validation constants shared by the three POST actions ----
    private const int ImprovementRiskFieldMax = 1000;

    /// <summary>
    /// Translates a raw DbUpdateException into a friendly "already linked"
    /// message when it was caused by the composite PK on ImprovementRisks.
    /// SQL Server raises error number 2627 (PK violation) or 2601 (unique
    /// index violation) when a concurrent writer beat us to the insert after
    /// our check-then-insert EXISTS check passed.
    /// </summary>
    private static bool IsPrimaryKeyViolation(DbUpdateException ex)
    {
        if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql)
        {
            return sql.Number == 2627 || sql.Number == 2601;
        }
        return false;
    }

    private static bool TryValidateLinkFields(int? expectedRiskReduction, string? impactDescription, string? notes, out string? error)
    {
        if (expectedRiskReduction.HasValue && (expectedRiskReduction.Value < 0 || expectedRiskReduction.Value > 100))
        {
            error = "Expected risk reduction must be between 0 and 100.";
            return false;
        }
        if (!string.IsNullOrEmpty(impactDescription) && impactDescription.Length > ImprovementRiskFieldMax)
        {
            error = $"Impact description must be {ImprovementRiskFieldMax} characters or fewer.";
            return false;
        }
        if (!string.IsNullOrEmpty(notes) && notes.Length > ImprovementRiskFieldMax)
        {
            error = $"Notes must be {ImprovementRiskFieldMax} characters or fewer.";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>
    /// Link an enterprise risk to an improvement initiative. Mirrors the
    /// ServicesController.LinkRisk pattern; the PManagement project uses the
    /// same approach (InitiativeRiskLink).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> LinkRisk(
        string improvementId,
        string riskId,
        string relationshipType = "Mitigates",
        int? expectedRiskReduction = null,
        string? impactDescription = null,
        string? notes = null)
    {
        if (!TryValidateLinkFields(expectedRiskReduction, impactDescription, notes, out var validationError))
            return Json(new { success = false, message = validationError });

        try
        {
            var improvement = await _context.ImprovementInitiatives.FindAsync(improvementId);
            if (improvement == null || improvement.IsDeleted)
                return Json(new { success = false, message = "Improvement not found." });

            // F-006: record-level scope (IDOR) on the owning initiative.
            if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement))
                return Json(new { success = false, message = "Improvement not found." });

            var risk = await _context.EnterpriseRisks.FindAsync(riskId);
            if (risk == null || risk.IsDeleted)
                return Json(new { success = false, message = "Risk not found." });

            var existing = await _context.ImprovementRisks
                .FirstOrDefaultAsync(ir => ir.ImprovementId == improvementId && ir.RiskId == riskId);
            if (existing != null)
                return Json(new { success = false, message = "This risk is already linked to this improvement." });

            var link = new ImprovementRisk
            {
                ImprovementId = improvementId,
                RiskId = riskId,
                RelationshipType = string.IsNullOrWhiteSpace(relationshipType) ? "Mitigates" : relationshipType,
                ExpectedRiskReduction = expectedRiskReduction,
                ImpactDescription = impactDescription,
                Notes = notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedById = User.Identity?.Name,
                UpdatedById = User.Identity?.Name
            };

            _context.ImprovementRisks.Add(link);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Risk linked successfully." });
        }
        catch (DbUpdateException ex) when (IsPrimaryKeyViolation(ex))
        {
            // Concurrent insert won the race — treat as duplicate, not as error
            return Json(new { success = false, message = "This risk is already linked to this improvement." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking risk {RiskId} to improvement {ImprovementId}", riskId, improvementId);
            return Json(new { success = false, message = "An error occurred while linking the risk." });
        }
    }

    /// <summary>
    /// Unlink a risk from an improvement initiative.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> UnlinkRisk(string improvementId, string riskId)
    {
        try
        {
            // F-006: record-level scope (IDOR) on the owning initiative.
            var owner = await FindInitiativeAsync(improvementId);
            if (owner != null && !(await _scopingService.GetScopeAsync(User)).CanAccess(owner))
                return Json(new { success = false, message = "Risk link not found." });

            var link = await _context.ImprovementRisks
                .FirstOrDefaultAsync(ir => ir.ImprovementId == improvementId && ir.RiskId == riskId);
            if (link == null)
                return Json(new { success = false, message = "Risk link not found." });

            _context.ImprovementRisks.Remove(link);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Risk unlinked successfully." });
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another thread already deleted the row
            return Json(new { success = false, message = "Risk link not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking risk {RiskId} from improvement {ImprovementId}", riskId, improvementId);
            return Json(new { success = false, message = "An error occurred while unlinking the risk." });
        }
    }

    /// <summary>
    /// Update the details of an existing improvement-risk link.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Improvement.Edit)]
    public async Task<IActionResult> UpdateImprovementRisk(
        string improvementId,
        string riskId,
        string relationshipType,
        int? expectedRiskReduction = null,
        string? impactDescription = null,
        string? notes = null)
    {
        if (!TryValidateLinkFields(expectedRiskReduction, impactDescription, notes, out var validationError))
            return Json(new { success = false, message = validationError });

        try
        {
            // F-006: record-level scope (IDOR) on the owning initiative.
            var owner = await FindInitiativeAsync(improvementId);
            if (owner != null && !(await _scopingService.GetScopeAsync(User)).CanAccess(owner))
                return Json(new { success = false, message = "Risk link not found." });

            var link = await _context.ImprovementRisks
                .FirstOrDefaultAsync(ir => ir.ImprovementId == improvementId && ir.RiskId == riskId);
            if (link == null)
                return Json(new { success = false, message = "Risk link not found." });

            link.RelationshipType = string.IsNullOrWhiteSpace(relationshipType) ? "Mitigates" : relationshipType;
            link.ExpectedRiskReduction = expectedRiskReduction;
            link.ImpactDescription = impactDescription;
            link.Notes = notes;
            link.UpdatedAt = DateTime.UtcNow;
            link.UpdatedById = User.Identity?.Name;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Relationship updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating improvement-risk for {ImprovementId}/{RiskId}", improvementId, riskId);
            return Json(new { success = false, message = "An error occurred while updating the relationship." });
        }
    }

    /// <summary>
    /// Get risks that are not yet linked to this improvement.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableRisks(string improvementId)
    {
        try
        {
            // F-006: record-level scope (IDOR) on the owning initiative —
            // return an empty list (don't leak risks) for an out-of-scope parent.
            var owner = await FindInitiativeAsync(improvementId);
            if (owner != null && !(await _scopingService.GetScopeAsync(User)).CanAccess(owner))
                return Json(new List<object>());

            var linkedRiskIds = await _context.ImprovementRisks
                .Where(ir => ir.ImprovementId == improvementId)
                .Select(ir => ir.RiskId)
                .ToListAsync();

            var scope = await _scopingService.GetScopeAsync(User);
            var available = await _context.EnterpriseRisks
                .Where(r => !r.IsDeleted && r.IsActive && !linkedRiskIds.Contains(r.Id))
                .ApplyOrganizationScope(scope)
                .Include(r => r.Category)
                .OrderByDescending(r => r.InherentRiskScore)
                .ThenBy(r => r.RiskNumber)
                .Select(r => new
                {
                    r.Id,
                    r.RiskNumber,
                    r.NameEn,
                    r.NameAr,
                    CategoryName = r.Category != null ? r.Category.NameEn : "",
                    r.RiskLevel,
                    r.InherentRiskScore,
                    r.ResidualRiskScore
                })
                .ToListAsync();

            return Json(available);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available risks for improvement {ImprovementId}", improvementId);
            return Json(new List<object>());
        }
    }

    /// <summary>
    /// Get all risks currently linked to an improvement (for the "Linked
    /// Risks" tab on the improvement details page).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLinkedRisks(string improvementId)
    {
        try
        {
            // F-006: record-level scope (IDOR) on the owning initiative —
            // return an empty list (don't leak links) for an out-of-scope parent.
            var owner = await FindInitiativeAsync(improvementId);
            if (owner != null && !(await _scopingService.GetScopeAsync(User)).CanAccess(owner))
                return Json(new List<object>());

            var links = await _context.ImprovementRisks
                .Where(ir => ir.ImprovementId == improvementId)
                .Include(ir => ir.Risk)
                    .ThenInclude(r => r!.Category)
                .OrderByDescending(ir => ir.Risk!.InherentRiskScore)
                .Select(ir => new
                {
                    ir.RiskId,
                    ir.RelationshipType,
                    ir.ExpectedRiskReduction,
                    ir.ImpactDescription,
                    ir.Notes,
                    ir.IsActive,
                    Risk = new
                    {
                        ir.Risk!.Id,
                        ir.Risk.RiskNumber,
                        ir.Risk.NameEn,
                        ir.Risk.NameAr,
                        ir.Risk.InherentRiskScore,
                        ir.Risk.ResidualRiskScore,
                        ir.Risk.RiskLevel,
                        CategoryName = ir.Risk.Category != null ? ir.Risk.Category.NameEn : ""
                    }
                })
                .ToListAsync();

            return Json(links);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading linked risks for improvement {ImprovementId}", improvementId);
            return Json(new List<object>());
        }
    }

    #endregion

    private sealed record ImprovementScopeProbe(int? OwningUnitId)
        : ESEMS.Web.Models.Common.IOwnedByUnit;
}

public class UpdateStatusRequest
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class DeleteMeasurementRequest
{
    public string Id { get; set; } = string.Empty;
}

