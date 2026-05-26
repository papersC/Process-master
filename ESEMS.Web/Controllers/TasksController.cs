using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Common;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for APQC Level 5 - Tasks (ProcessTask)
/// </summary>
[Authorize(Policy = AppPolicies.Module.WorkflowTask.View)]
public class TasksController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TasksController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IScopingService _scopingService;

    public TasksController(ApplicationDbContext context, ILogger<TasksController> logger, IStringLocalizer<SharedResource> localizer, IScopingService scopingService)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _scopingService = scopingService;
    }

    /// <summary>
    /// F-020: per-record IDOR guard for a ProcessTask, resolved through its
    /// parent process (ProcessTask → Activity → Process, which is IOwnedByUnit).
    /// A task whose parent process is out of scope is treated as inaccessible.
    /// Tasks with no resolvable parent process fall back to the task's own
    /// OwningUnitId. Null owning unit → orphan, visible to everyone (matches the
    /// ScopeContext.CanAccess contract). Mirrors AIBpmnReadController (SEC-003).
    /// </summary>
    private bool CanAccessTask(ProcessTask task, ScopeContext scope)
    {
        if (scope.IsUnscoped) return true;
        var owningUnitId = task.Activity?.Process != null
            ? task.Activity.Process.OwningUnitId
            : task.OwningUnitId;
        if (owningUnitId == null) return true;
        return scope.VisibleUnitIds != null && scope.VisibleUnitIds.Contains(owningUnitId.Value);
    }

    // Tasks are always scoped to a parent Activity/Process. /Tasks bare
    // has no meaningful list view — send the user to Processes where they
    // can drill in, so a typed/bookmarked URL doesn't 404.
    public IActionResult Index() => RedirectToAction("Index", "Processes");

    /// <summary>
    /// Read-only task details. Restores the drill-down pattern that every
    /// other entity already had — Process, Activity, Service, Asset, Risk,
    /// OrganizationUnit all have a Details view; Task didn't, breaking
    /// inline navigation from search results, audit logs, and the new
    /// /OrganizationUnits/Details "Owned Tasks" cards.
    ///
    /// Shows parent activity + process breadcrumb, owner unit, system,
    /// status flags, automation classification, RACI assignments (live
    /// from TaskRaci), and the task's BPMN diagram if one exists.
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var task = await _context.ProcessTasks
            .Include(t => t.Activity)
                .ThenInclude(a => a!.Process)
                    .ThenInclude(p => p!.ProcessGroup)
            .Include(t => t.OwningUnit)
            .Include(t => t.System)
            .Include(t => t.RaciMatrix)
                .ThenInclude(r => r.OrganizationUnit)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (task == null) return NotFound();

        // F-020: record-level scope (IDOR) via the task's parent process.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!CanAccessTask(task, scope))
            return NotFound();

        return View(task);
    }

    /// <summary>
    /// Create task form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.WorkflowTask.Create)]
    public async Task<IActionResult> Create(string? activityId = null)
    {
        await PopulateDropdowns();
        var model = new ProcessTask();
        if (!string.IsNullOrEmpty(activityId))
        {
            model.ActivityId = activityId;
            var activity = await _context.Activities.Include(a => a.Process).FirstOrDefaultAsync(a => a.Id == activityId);
            if (activity != null)
            {
                var existingCount = await _context.ProcessTasks
                    .CountAsync(t => t.ActivityId == activityId && !t.IsDeleted);
                model.Code = $"{activity.Code}.{existingCount + 1}";
                model.DisplayOrder = existingCount + 1;
                ViewBag.ParentProcessId = activity.ProcessId;
            }
        }
        return View(model);
    }

    /// <summary>
    /// Create task
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.WorkflowTask.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProcessTask task)
    {
        if (ModelState.IsValid)
        {
            task.Id = Guid.NewGuid().ToString();
            task.CreatedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;
            task.CreatedById = User.Identity?.Name;

            // Auto-derive: IsAutomated from AutomationStatus
            task.IsAutomated = task.AutomationStatus == AutomationStatus.Automated;

            // Auto-derive: DigitalSystemName from linked SystemDefinition
            if (!string.IsNullOrEmpty(task.SystemId))
            {
                var system = await _context.SystemDefinitions.FindAsync(task.SystemId);
                if (system != null)
                {
                    var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
                    task.DigitalSystemName = isArabic ? system.NameAr : system.NameEn;
                }
            }

            // DisplayOrder is auto-assigned (form input is hidden) — next slot within the parent activity.
            if (task.DisplayOrder <= 0)
            {
                var nextOrder = await _context.ProcessTasks
                    .Where(t => t.ActivityId == task.ActivityId)
                    .MaxAsync(t => (int?)t.DisplayOrder) ?? 0;
                task.DisplayOrder = nextOrder + 1;
            }

            _context.ProcessTasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Task {Code} created by {User}", task.Code, User.Identity?.Name);
            TempData["Success"] = _localizer["Success_TaskCreated"].Value;

            var activity = await _context.Activities.FindAsync(task.ActivityId);
            return RedirectToAction("Details", "Processes", new { id = activity?.ProcessId });
        }

        await PopulateDropdowns();
        return View(task);
    }

    /// <summary>
    /// Edit task form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.WorkflowTask.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var task = await _context.ProcessTasks
            .Include(t => t.Activity).ThenInclude(a => a.Process)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

        if (task == null)
            return NotFound();

        // F-020: record-level scope (IDOR) via the task's parent process.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!CanAccessTask(task, scope))
            return NotFound();

        ViewBag.ParentProcessId = task.Activity?.ProcessId;
        await PopulateDropdowns();
        return View(task);
    }

    /// <summary>
    /// Edit task
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.WorkflowTask.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ProcessTask task)
    {
        if (id != task.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            // FUNC-001 mass-assignment defense: load the tracked entity and copy
            // only user-editable fields. _context.Update(task) overwrote every
            // column from the post — a crafted body could flip IsDeleted=true,
            // forge CreatedById/CreatedAt/DeletedAt/Version, or wipe
            // BpmnDiagram/AutomationAssessmentScores (which this form never
            // sends). DisplayOrder is server-managed and keeps its persisted
            // value; IsAutomated + DigitalSystemName stay server-derived below.
            var existing = await _context.ProcessTasks
                .Include(t => t.Activity).ThenInclude(a => a!.Process)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
            if (existing == null)
                return NotFound();

            // F-020: record-level scope (IDOR) on POST — gate on the PERSISTED
            // task's parent process, not whatever the form posted.
            var scope = await _scopingService.GetScopeAsync(User);
            if (!CanAccessTask(existing, scope))
                return NotFound();

            existing.Code                  = task.Code;
            existing.ActivityId            = task.ActivityId;
            existing.NameEn                = task.NameEn;
            existing.NameAr                = task.NameAr;
            existing.DescriptionEn         = task.DescriptionEn;
            existing.DescriptionAr         = task.DescriptionAr;
            existing.ChannelType           = task.ChannelType;
            existing.OwningUnitId          = task.OwningUnitId;
            existing.SystemId              = task.SystemId;
            existing.Tags                  = task.Tags;
            existing.ProcedureStatus       = task.ProcedureStatus;
            existing.AutomationStatus      = task.AutomationStatus;
            existing.AutomabilityStatus    = task.AutomabilityStatus;
            existing.CurrentProposedStatus = task.CurrentProposedStatus;
            existing.EstimatedDuration     = task.EstimatedDuration;
            existing.DurationUnit          = task.DurationUnit;
            existing.EstimatedCost         = task.EstimatedCost;
            existing.LinkedServices        = task.LinkedServices;
            existing.DocumentReference     = task.DocumentReference;
            existing.DocumentLanguage      = task.DocumentLanguage;
            existing.UpdatedAt             = DateTime.UtcNow;
            existing.UpdatedById           = User.Identity?.Name;
            existing.Version++;

            // Auto-derive: IsAutomated from AutomationStatus
            existing.IsAutomated = existing.AutomationStatus == AutomationStatus.Automated;

            // Auto-derive: DigitalSystemName from linked SystemDefinition
            if (!string.IsNullOrEmpty(existing.SystemId))
            {
                var system = await _context.SystemDefinitions.FindAsync(existing.SystemId);
                if (system != null)
                {
                    var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
                    existing.DigitalSystemName = isArabic ? system.NameAr : system.NameEn;
                }
            }
            else
            {
                existing.DigitalSystemName = null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Task {Code} updated by {User}", existing.Code, User.Identity?.Name);
            TempData["Success"] = _localizer["Success_TaskUpdated"].Value;

            var activity = await _context.Activities.FindAsync(existing.ActivityId);
            return RedirectToAction("Details", "Processes", new { id = activity?.ProcessId });
        }

        await PopulateDropdowns();
        return View(task);
    }

    /// <summary>
    /// Delete task (soft delete)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.WorkflowTask.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var task = await _context.ProcessTasks
            .Include(t => t.Activity).ThenInclude(a => a!.Process)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

        if (task == null)
            return NotFound();

        // F-020: record-level scope (IDOR) via the task's parent process.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!CanAccessTask(task, scope))
            return NotFound();

        task.IsDeleted = true;
        task.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {Code} deleted by {User}", task.Code, User.Identity?.Name);
        TempData["Success"] = _localizer["Success_TaskDeleted"].Value;
        return RedirectToAction("Details", "Processes", new { id = task.Activity?.ProcessId });
    }

    private async Task PopulateDropdowns()
    {
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");

        var activities = await _context.Activities
            .Where(a => !a.IsDeleted)
            .Include(a => a.Process)
            .OrderBy(a => a.Code)
            .ToListAsync();
        ViewBag.Activities = new SelectList(
            activities.Select(a => new { a.Id, DisplayName = $"{a.Code} - {(isArabic ? a.NameAr : a.NameEn)}" }),
            "Id", "DisplayName");

        var orgUnits = await _context.OrganizationUnits
            .Where(u => !u.IsDeleted && u.IsActive)
            .OrderBy(u => u.Level).ThenBy(u => u.NameEn)
            .ToListAsync();
        ViewBag.OrganizationUnits = new SelectList(
            orgUnits.Select(u => new { u.Id, DisplayName = isArabic ? u.NameAr : u.NameEn }),
            "Id", "DisplayName");

        var systems = await _context.SystemDefinitions
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.NameEn)
            .ToListAsync();
        ViewBag.Systems = new SelectList(
            systems.Select(s => new { s.Id, DisplayName = isArabic ? s.NameAr : s.NameEn }),
            "Id", "DisplayName");
    }
}

