using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Models.RiskManagement;
using ESEMS.Web.Extensions;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Common;
using ESEMS.Web.Services.Workflow;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for Change Requests management
/// </summary>
[Authorize(Policy = AppPolicies.Module.ChangeRequest.View)]
public class ChangeRequestsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChangeRequestsController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IWorkflowService _workflowService;
    private readonly IScopingService _scopingService;

    public ChangeRequestsController(
        ApplicationDbContext context,
        ILogger<ChangeRequestsController> logger,
        IStringLocalizer<SharedResource> localizer,
        IWorkflowService workflowService,
        IScopingService scopingService)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _workflowService = workflowService;
        _scopingService = scopingService;
    }

    /// <summary>
    /// List all change requests
    /// </summary>
    public async Task<IActionResult> Index(ChangeRequestStatus? status = null, string? processId = null, string? serviceId = null)
    {
        var query = _context.ChangeRequests
            .Where(cr => !cr.IsDeleted)
            .Include(cr => cr.Process)
            .Include(cr => cr.Service)
            .AsQueryable();

        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyOwningUnitScope(scope);

        if (status.HasValue)
            query = query.Where(cr => cr.Status == status.Value);

        // F-CC-002: support deep-link from /Processes/Details or /Services/Details
        // so users can jump to the filtered CR list for a specific entity.
        if (!string.IsNullOrWhiteSpace(processId))
            query = query.Where(cr => cr.ProcessId == processId);
        if (!string.IsNullOrWhiteSpace(serviceId))
            query = query.Where(cr => cr.ServiceId == serviceId);

        var changeRequests = await query.OrderByDescending(cr => cr.CreatedAt).ToListAsync();

        ViewBag.SelectedStatus = status;
        ViewBag.FilterProcessId = processId;
        ViewBag.FilterServiceId = serviceId;
        return View(changeRequests);
    }

    /// <summary>
    /// View change request details
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var changeRequest = await _context.ChangeRequests
            .Include(cr => cr.Process)
                .ThenInclude(p => p!.ProcessGroup)
            .Include(cr => cr.Service)
            .Include(cr => cr.OwningUnit)
            .Include(cr => cr.Comments)
            .Include(cr => cr.ChangeRequestAssets)
                .ThenInclude(cra => cra.Asset)
                    .ThenInclude(a => a!.Category)
            .Include(cr => cr.ChangeRequestRisks)
                .ThenInclude(crr => crr.Risk)
                    .ThenInclude(r => r!.Category)
            .FirstOrDefaultAsync(cr => cr.Id == id && !cr.IsDeleted);

        if (changeRequest == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(changeRequest))
            return NotFound();

        return View(changeRequest);
    }

    /// <summary>
    /// Create change request form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.ChangeRequest.Create)]
    public async Task<IActionResult> Create(string? processId = null, string? serviceId = null)
    {
        await PopulateDropdowns();
        var model = new ChangeRequest();
        if (!string.IsNullOrEmpty(processId))
            model.ProcessId = processId;
        if (!string.IsNullOrEmpty(serviceId))
            model.ServiceId = serviceId;
        return View(model);
    }

    /// <summary>
    /// Create change request
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.ChangeRequest.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChangeRequest changeRequest, string[]? selectedAssets, string[]? selectedRisks)
    {
        // Code and DisplayOrder are system-managed — strip them from
        // ModelState so the hidden-value-missing branch doesn't reject a
        // valid form.
        ModelState.Remove(nameof(ChangeRequest.Code));
        ModelState.Remove(nameof(ChangeRequest.DisplayOrder));

        if (ModelState.IsValid)
        {
            changeRequest.Id = Guid.NewGuid().ToString();
            changeRequest.Code = await GenerateNextChangeRequestCodeAsync();
            changeRequest.DisplayOrder = await GetNextDisplayOrderAsync();
            changeRequest.CreatedAt = DateTime.UtcNow;
            changeRequest.UpdatedAt = DateTime.UtcNow;
            changeRequest.CreatedById = User.Identity?.Name;
            changeRequest.Status = ChangeRequestStatus.Submitted;
            // Mass-assignment defense: a hostile client could POST IsDeleted=true
            // / ApprovedById=victim / ApprovalDate=… via extra form fields. Reset
            // every privileged field explicitly so the model binder can't smuggle
            // them in. Verified by MassAssignmentTests.ChangeRequestCreate_*.
            changeRequest.IsDeleted = false;
            changeRequest.ApprovedById = null;
            changeRequest.ApprovalDate = null;
            changeRequest.RejectionReason = null;

            _context.ChangeRequests.Add(changeRequest);

            // Add asset relationships
            if (selectedAssets != null && selectedAssets.Length > 0)
            {
                foreach (var assetId in selectedAssets)
                {
                    var relationship = new ChangeRequestAsset
                    {
                        ChangeRequestId = changeRequest.Id,
                        AssetId = assetId,
                        ImpactType = "Modify",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Set<ChangeRequestAsset>().Add(relationship);
                }
            }

            // Add risk relationships
            if (selectedRisks != null && selectedRisks.Length > 0)
            {
                foreach (var riskId in selectedRisks)
                {
                    var relationship = new ChangeRequestRisk
                    {
                        ChangeRequestId = changeRequest.Id,
                        RiskId = riskId,
                        RelationshipType = "Affects",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Set<ChangeRequestRisk>().Add(relationship);
                }
            }

            await _context.SaveChangesAsync();

            // Submit to approval workflow
            try
            {
                var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
                var userName = User.Identity?.Name ?? "Unknown";
                await _workflowService.CreateAsync(changeRequest.Id, "ChangeRequest", userId, userName,
                    $"{changeRequest.NameEn}: {changeRequest.Justification}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create approval workflow for change request {Id}", changeRequest.Id);
            }

            TempData["Success"] = _localizer["Success_ChangeRequestCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = changeRequest.Id });
        }

        await PopulateDropdowns();
        return View(changeRequest);
    }

    /// <summary>
    /// Edit change request — only allowed while the request is still pre-decision
    /// (Submitted or Rejected). Approved / UnderReview / Implemented / Cancelled
    /// requests are workflow-managed and must not be mutated by the edit form.
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.ChangeRequest.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var changeRequest = await _context.ChangeRequests
            .Include(cr => cr.ChangeRequestAssets)
            .Include(cr => cr.ChangeRequestRisks)
            .FirstOrDefaultAsync(cr => cr.Id == id && !cr.IsDeleted);

        if (changeRequest == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(changeRequest))
            return NotFound();

        if (!IsEditable(changeRequest.Status))
        {
            TempData["Error"] = _localizer["Error_ChangeRequestNotEditable"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        await PopulateDropdowns();
        ViewBag.SelectedAssetIds = changeRequest.ChangeRequestAssets.Select(cra => cra.AssetId).ToHashSet();
        ViewBag.SelectedRiskIds  = changeRequest.ChangeRequestRisks.Select(crr => crr.RiskId).ToHashSet();
        return View(changeRequest);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.ChangeRequest.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ChangeRequest form, string[]? selectedAssets, string[]? selectedRisks)
    {
        if (id != form.Id) return BadRequest();

        // System-managed fields — we never read them from the form.
        ModelState.Remove(nameof(ChangeRequest.Code));
        ModelState.Remove(nameof(ChangeRequest.DisplayOrder));

        var changeRequest = await _context.ChangeRequests
            .Include(cr => cr.ChangeRequestAssets)
            .Include(cr => cr.ChangeRequestRisks)
            .FirstOrDefaultAsync(cr => cr.Id == id && !cr.IsDeleted);

        if (changeRequest == null)
            return NotFound();

        // IDOR guard on POST — checks the persisted OwningUnitId. The 'form'
        // input is untrusted; we can use 'changeRequest' here because we
        // re-loaded from the DB above.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(changeRequest))
            return NotFound();

        if (!IsEditable(changeRequest.Status))
        {
            TempData["Error"] = _localizer["Error_ChangeRequestNotEditable"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            ViewBag.SelectedAssetIds = (selectedAssets ?? Array.Empty<string>()).ToHashSet();
            ViewBag.SelectedRiskIds  = (selectedRisks  ?? Array.Empty<string>()).ToHashSet();
            return View(form);
        }

        // Patch only user-editable fields — never let the form overwrite
        // Status, ApprovedById, ApprovalDate, ImplementationDate or
        // RejectionReason (those are set by the approval workflow). Code
        // and DisplayOrder are system-managed too — they stay whatever the
        // Create flow assigned.
        changeRequest.Source              = form.Source;
        changeRequest.Priority            = form.Priority;
        changeRequest.NameEn              = form.NameEn;
        changeRequest.NameAr              = form.NameAr;
        changeRequest.DescriptionEn       = form.DescriptionEn;
        changeRequest.DescriptionAr       = form.DescriptionAr;
        changeRequest.ProcessId           = form.ProcessId;
        changeRequest.ServiceId           = form.ServiceId;
        changeRequest.OwningUnitId        = form.OwningUnitId;
        changeRequest.Justification       = form.Justification;
        changeRequest.ImpactAssessment    = form.ImpactAssessment;
        changeRequest.ExternalReferenceId = form.ExternalReferenceId;
        changeRequest.UpdatedAt           = DateTime.UtcNow;
        changeRequest.UpdatedById         = User.Identity?.Name;

        // Resync asset/risk junction rows: drop all, re-add from the posted
        // selection. Matches the Create flow's behavior and avoids having to
        // diff the two sets manually.
        _context.Set<ChangeRequestAsset>().RemoveRange(changeRequest.ChangeRequestAssets);
        _context.Set<ChangeRequestRisk>().RemoveRange(changeRequest.ChangeRequestRisks);

        if (selectedAssets != null)
        {
            foreach (var assetId in selectedAssets.Distinct())
            {
                _context.Set<ChangeRequestAsset>().Add(new ChangeRequestAsset
                {
                    ChangeRequestId = changeRequest.Id,
                    AssetId = assetId,
                    ImpactType = "Modify",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedById = User.Identity?.Name
                });
            }
        }

        if (selectedRisks != null)
        {
            foreach (var riskId in selectedRisks.Distinct())
            {
                _context.Set<ChangeRequestRisk>().Add(new ChangeRequestRisk
                {
                    ChangeRequestId = changeRequest.Id,
                    RiskId = riskId,
                    RelationshipType = "Affects",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedById = User.Identity?.Name
                });
            }
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_ChangeRequestUpdated"].Value;
        return RedirectToAction(nameof(Details), new { id = changeRequest.Id });
    }

    private static bool IsEditable(ChangeRequestStatus status)
        => status == ChangeRequestStatus.Submitted || status == ChangeRequestStatus.Rejected;

    /// <summary>
    /// FLOW-002 / FLOW-009: keep the linked <see cref="Models.Workflow.WorkflowInstance"/>
    /// in lockstep with the ChangeRequest's own status. The CR was historically
    /// driven by ad-hoc status flips on the controller while the workflow row
    /// created at submission time was never advanced — leaving an orphaned
    /// <c>Submitted</c> workflow even after the CR was Approved / Rejected /
    /// Implemented / Cancelled.
    ///
    /// The ChangeRequest approval model is single-step: one Approve / Reject /
    /// Cancel action is terminal for the CR. So rather than routing through the
    /// multi-level engine (which could advance a Level2-required rule to a
    /// second approver while the CR is already Approved, re-introducing the very
    /// desync we're fixing), we close the open workflow row directly to the
    /// status that mirrors the CR. The submitter≠approver guard for CRs is
    /// enforced at the controller (see Approve), and inbox-driven CR actions
    /// still flow through the engine's central guards via WorkflowController.
    ///
    /// <paramref name="workflowAction"/> is "Approved" / "Rejected" /
    /// "ReturnedForCorrection"; pass null for terminal CR states with no direct
    /// verb (Implemented, Cancelled) — those close the workflow as Cancelled.
    /// </summary>
    private async Task SyncWorkflowAsync(string changeRequestId, string? workflowAction, string? comments)
    {
        try
        {
            var workflow = (await _workflowService.GetByEntityAsync(changeRequestId, "ChangeRequest"))
                .FirstOrDefault(w => w.Status == Models.Workflow.WorkflowStatus.Submitted
                                  || w.Status == Models.Workflow.WorkflowStatus.UnderReview);
            if (workflow == null) return; // no open workflow (e.g. config-less CR) — nothing to sync.

            // Close the workflow row to mirror the CR's new terminal status.
            workflow.Status = workflowAction switch
            {
                "Approved" => Models.Workflow.WorkflowStatus.Approved,
                "Rejected" => Models.Workflow.WorkflowStatus.Rejected,
                "ReturnedForCorrection" => Models.Workflow.WorkflowStatus.ReturnedForCorrection,
                _ => Models.Workflow.WorkflowStatus.Cancelled
            };
            workflow.UpdatedAt = DateTime.UtcNow;

            // Stamp the action onto the current pending step so the workflow's
            // own audit trail isn't left dangling on "Pending".
            var step = workflow.Steps?.FirstOrDefault(s => s.StepLevel == workflow.CurrentLevel && s.Action == "Pending");
            if (step != null)
            {
                step.Action = workflowAction ?? "Cancelled";
                step.ApproverUserId = GetCurrentUserId();
                step.ApproverName = User.Identity?.Name;
                step.Comments = comments;
                step.ActionDate = DateTime.UtcNow;
            }

            _context.Update(workflow);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never let a workflow-sync failure mask the primary CR action;
            // log and continue. The CR status is the source of truth for the UI.
            _logger.LogWarning(ex, "Failed to sync workflow for change request {Id} (action {Action})", changeRequestId, workflowAction);
        }
    }

    private int GetCurrentUserId()
        => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // A change request can only be approved or rejected while it's still
    // pending — re-approving an already-Approved request or rejecting an
    // Implemented one used to silently overwrite the audit fields and
    // flip the status backwards.
    private static bool IsActionable(ChangeRequestStatus status)
        => status == ChangeRequestStatus.Submitted || status == ChangeRequestStatus.UnderReview;

    /// <summary>
    /// Transitions a Submitted CR into the UnderReview state. The audit-flagged
    /// gap (2026-05-19 QA): the controller exposed Approve / Reject from the
    /// detail page but nothing actually flipped Submitted → UnderReview, so
    /// the intermediate "I'm picking this up" step was invisible. Same auth
    /// + scope rules as Approve/Reject; refuses any non-Submitted state.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.ChangeRequest.Approve)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartReview(string id)
    {
        var changeRequest = await _context.ChangeRequests.FindAsync(id);
        if (changeRequest == null)
            return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(changeRequest))
            return NotFound();
        if (changeRequest.Status != ChangeRequestStatus.Submitted)
        {
            TempData["Error"] = $"Cannot start review on a change request in status '{changeRequest.Status}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        changeRequest.Status = ChangeRequestStatus.UnderReview;
        changeRequest.ReviewStartedById = User.Identity?.Name;
        changeRequest.ReviewStartedAt = DateTime.UtcNow;
        changeRequest.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // FLOW-002 / FLOW-009: mirror the "picked up" state onto the workflow
        // row. The engine has no plain "start review" verb (it only acts on
        // Approve/Reject/Return), so nudge the open workflow's status directly
        // so PendingApprovals and the CR stay consistent.
        var reviewWorkflow = (await _workflowService.GetByEntityAsync(id, "ChangeRequest"))
            .FirstOrDefault(w => w.Status == Models.Workflow.WorkflowStatus.Submitted);
        if (reviewWorkflow != null)
        {
            reviewWorkflow.Status = Models.Workflow.WorkflowStatus.UnderReview;
            reviewWorkflow.UpdatedAt = DateTime.UtcNow;
            _context.Update(reviewWorkflow);
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Change request moved to Under Review.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Mark an Approved change request as Implemented. Closes out the
    /// lifecycle — no further transitions from Implemented. Captures who
    /// marked it and when so reports can answer "what changes shipped
    /// in Q2 and who delivered them?".
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.ChangeRequest.Approve)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkImplemented(string id)
    {
        var changeRequest = await _context.ChangeRequests.FindAsync(id);
        if (changeRequest == null)
            return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(changeRequest))
            return NotFound();
        if (changeRequest.Status != ChangeRequestStatus.Approved)
        {
            TempData["Error"] = $"Only Approved change requests can be marked Implemented; this one is '{changeRequest.Status}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        changeRequest.Status = ChangeRequestStatus.Implemented;
        changeRequest.ImplementedById = User.Identity?.Name;
        changeRequest.ImplementationDate = DateTime.UtcNow;
        changeRequest.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // FLOW-009: a normal flow closes the workflow at Approve time, but
        // close any still-open row defensively so a terminal CR never leaves
        // an orphaned Submitted/UnderReview workflow behind.
        await SyncWorkflowAsync(id, null, null);

        TempData["Success"] = "Change request marked as Implemented.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Cancel a change request from any non-terminal state (Submitted,
    /// UnderReview, or Approved). Captures the cancellation reason so
    /// downstream reports can distinguish "rejected by reviewer" from
    /// "withdrawn by requester" from "deprioritised by leadership".
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.ChangeRequest.Approve)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string id, string? cancellationReason)
    {
        var changeRequest = await _context.ChangeRequests.FindAsync(id);
        if (changeRequest == null)
            return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(changeRequest))
            return NotFound();
        var s = changeRequest.Status;
        if (s != ChangeRequestStatus.Submitted
         && s != ChangeRequestStatus.UnderReview
         && s != ChangeRequestStatus.Approved)
        {
            TempData["Error"] = $"Cannot cancel a change request in status '{s}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        changeRequest.Status = ChangeRequestStatus.Cancelled;
        changeRequest.CancelledById = User.Identity?.Name;
        changeRequest.CancelledAt = DateTime.UtcNow;
        changeRequest.CancellationReason = string.IsNullOrWhiteSpace(cancellationReason)
            ? null
            : cancellationReason.Trim();
        changeRequest.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // FLOW-002 / FLOW-009: a cancelled CR is terminal — close any open
        // workflow row so it doesn't linger in PendingApprovals.
        await SyncWorkflowAsync(id, null, cancellationReason);

        TempData["Success"] = "Change request cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Approve change request
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.ChangeRequest.Approve)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id)
    {
        var changeRequest = await _context.ChangeRequests.FindAsync(id);
        if (changeRequest == null)
            return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(changeRequest))
            return NotFound();
        if (!IsActionable(changeRequest.Status))
        {
            TempData["Error"] = $"Cannot approve a change request in status '{changeRequest.Status}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // FLOW-001 (self-approval): the requester must not approve their own
        // change request. The CR carries no submitter id of its own, so the
        // authoritative submitter is the linked workflow's SubmittedById.
        var approverId = GetCurrentUserId();
        var crWorkflow = (await _workflowService.GetByEntityAsync(id, "ChangeRequest"))
            .FirstOrDefault(w => w.Status == Models.Workflow.WorkflowStatus.Submitted
                              || w.Status == Models.Workflow.WorkflowStatus.UnderReview);
        if (crWorkflow?.SubmittedById == approverId && approverId != 0)
        {
            TempData["Error"] = "You cannot approve a change request that you submitted. Approval must be performed by a different user.";
            return RedirectToAction(nameof(Details), new { id });
        }

        changeRequest.Status = ChangeRequestStatus.Approved;
        changeRequest.ApprovedById = User.Identity?.Name;
        changeRequest.ApprovalDate = DateTime.UtcNow;
        changeRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // FLOW-002: advance the linked workflow row in lockstep.
        await SyncWorkflowAsync(id, "Approved", null);

        TempData["Success"] = _localizer["Success_ChangeRequestApproved"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Reject change request
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.ChangeRequest.Approve)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string id, string rejectionReason)
    {
        var changeRequest = await _context.ChangeRequests.FindAsync(id);
        if (changeRequest == null)
            return NotFound();
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(changeRequest))
            return NotFound();
        if (!IsActionable(changeRequest.Status))
        {
            TempData["Error"] = $"Cannot reject a change request in status '{changeRequest.Status}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        changeRequest.Status = ChangeRequestStatus.Rejected;
        changeRequest.RejectionReason = rejectionReason;
        changeRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // FLOW-002: close the linked workflow row in lockstep.
        await SyncWorkflowAsync(id, "Rejected", rejectionReason);

        TempData["Success"] = _localizer["Success_ChangeRequestRejected"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Next ChangeRequest code: CR-001, CR-002, ... Scans all rows including
    /// soft-deleted ones so a deleted CR-006 doesn't get reused and collide
    /// with any unique-index on Code.
    /// </summary>
    private async Task<string> GenerateNextChangeRequestCodeAsync()
    {
        var codes = await _context.ChangeRequests
            .IgnoreQueryFilters()
            .Where(c => c.Code.StartsWith("CR-"))
            .Select(c => c.Code)
            .ToListAsync();

        int max = 0;
        foreach (var code in codes)
        {
            if (int.TryParse(code[3..], out var n) && n > max) max = n;
        }
        return $"CR-{max + 1:D3}";
    }

    private async Task<int> GetNextDisplayOrderAsync()
    {
        var max = await _context.ChangeRequests
            .IgnoreQueryFilters()
            .MaxAsync(c => (int?)c.DisplayOrder) ?? 0;
        return max + 1;
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Processes = new SelectList(
            await _context.Processes.Where(p => !p.IsDeleted).ToListAsync(),
            "Id", "Name");
        ViewBag.Services = new SelectList(
            await _context.Services.Where(s => !s.IsDeleted).ToListAsync(),
            "Id", "Name");
        ViewBag.OrganizationUnits = new SelectList(
            await _context.OrganizationUnits.Where(u => !u.IsDeleted && u.IsActive).ToListAsync(),
            "Id", "Name");

        // Load assets for multi-select
        ViewBag.Assets = await _context.Assets
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Disposed)
            .Include(a => a.Category)
            .OrderBy(a => a.AssetTag)
            .ToListAsync();

        // Load risks for multi-select
        ViewBag.Risks = await _context.EnterpriseRisks
            .Where(r => !r.IsDeleted && r.IsActive)
            .OrderBy(r => r.RiskNumber)
            .ToListAsync();
    }
}

