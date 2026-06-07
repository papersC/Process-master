using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.ServiceManagement;
using ESEMS.Web.Models.DocumentManagement;
using ESEMS.Web.Extensions;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Common;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for APQC Level 3 - Processes
/// </summary>
[Authorize(Policy = AppPolicies.Module.Process.View)]
public class ProcessesController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProcessesController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IScopingService _scopingService;
    private readonly HierarchicalCodeService _codeSvc;

    public ProcessesController(ApplicationDbContext context, ILogger<ProcessesController> logger, IStringLocalizer<SharedResource> localizer, IScopingService scopingService, HierarchicalCodeService codeSvc)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _scopingService = scopingService;
        _codeSvc = codeSvc;
    }

    /// <summary>
    /// Process Management Dashboard/Overview
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        // SCOPE: dashboard KPIs + recent list must respect the user's data scope,
        // same as Index — otherwise a scoped user sees org-wide counts and recent
        // items from units they can't open. No-op for All-scope users.
        var scope = await _scopingService.GetScopeAsync(User);
        var scoped = _context.Processes.Where(p => !p.IsDeleted).ApplyOwningUnitScope(scope);

        var totalProcesses = await scoped.CountAsync();
        var activeProcesses = await scoped.CountAsync(p => p.Status == ProcessStatus.Active);
        var draftProcesses = await scoped.CountAsync(p => p.Status == ProcessStatus.Draft);
        var underImprovementProcesses = await scoped.CountAsync(p => p.Status == ProcessStatus.UnderImprovement);
        var totalCategories = await _context.Categories.CountAsync(c => !c.IsDeleted);
        var totalProcessGroups = await _context.ProcessGroups.CountAsync(pg => !pg.IsDeleted);

        ViewBag.TotalProcesses = totalProcesses;
        ViewBag.ActiveProcesses = activeProcesses;
        ViewBag.DraftProcesses = draftProcesses;
        ViewBag.UnderImprovementProcesses = underImprovementProcesses;
        ViewBag.TotalCategories = totalCategories;
        ViewBag.TotalProcessGroups = totalProcessGroups;

        // Get recent processes (scoped)
        var recentProcesses = await scoped
            .Include(p => p.ProcessGroup)
            .Include(p => p.OwningUnit)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(10)
            .ToListAsync();

        return View(recentProcesses);
    }

    /// <summary>
    /// List all processes
    /// </summary>
    public async Task<IActionResult> Index(string? processGroupId = null, ProcessStatus? status = null)
    {
        var query = _context.Processes
            .Where(p => !p.IsDeleted)
            .Include(p => p.ProcessGroup)
                .ThenInclude(pg => pg!.Category)
            .Include(p => p.OwningUnit)
            .Include(p => p.Service)
            .AsQueryable();

        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyOwningUnitScope(scope);

        if (!string.IsNullOrEmpty(processGroupId))
            query = query.Where(p => p.ProcessGroupId == processGroupId);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var processes = await query.OrderBy(p => p.ProcessGroup!.SortKey ?? p.ProcessGroup.Code)
            .ThenBy(p => p.DisplayOrder)
            .ToListAsync();

        ViewBag.ProcessGroups = await _context.ProcessGroups.Where(pg => !pg.IsDeleted).ToListAsync();
        ViewBag.SelectedProcessGroupId = processGroupId;
        ViewBag.SelectedStatus = status;

        // A Process is "has BPMN" if either:
        //   (a) Process.BpmnDiagram itself is populated (the canonical L3 diagram
        //       — what the library importer and the in-app editor write), or
        //   (b) any of its ProcessTasks (L5) carry a per-task BPMN diagram.
        // Previously we only checked (b), so any process whose diagram lived on
        // the Process row (the common case after the bulk library import) showed
        // an em-dash here and looked unimported.
        var processIdsWithProcessLevelBpmn = await _context.Processes
            .Where(p => !p.IsDeleted && p.BpmnDiagram != null && p.BpmnDiagram.Length > 10)
            .Select(p => p.Id)
            .ToListAsync();
        var processIdsWithTaskBpmn = await _context.ProcessTasks
            .Where(t => !t.IsDeleted && t.BpmnDiagram != null && t.BpmnDiagram.Length > 10)
            .Select(t => t.Activity!.ProcessId)
            .Distinct()
            .ToListAsync();
        var processIdsWithBpmn = new HashSet<string>(processIdsWithProcessLevelBpmn);
        foreach (var id in processIdsWithTaskBpmn) processIdsWithBpmn.Add(id);
        ViewBag.ProcessIdsWithBpmn = processIdsWithBpmn;

        return View(processes);
    }

    /// <summary>
    /// Debug action to check BPMN diagrams in database. Reverted from
    /// [AllowAnonymous] — was leaking process counts + BPMN presence to
    /// unauthenticated callers. Now inherits class-level
    /// Authorize(Policy = AppPolicies.Module.Process.View).
    /// </summary>
    public async Task<IActionResult> CheckBpmn()
    {
        var processesWithBpmn = await _context.Processes
            .Where(p => p.BpmnDiagram != null && p.BpmnDiagram.Length > 100)
            .Select(p => new { p.Code, p.NameEn, HasBpmn = p.BpmnDiagram != null, BpmnLength = p.BpmnDiagram!.Length })
            .ToListAsync();

        return Json(new {
            TotalProcesses = await _context.Processes.CountAsync(),
            ProcessesWithBpmn = processesWithBpmn.Count,
            Details = processesWithBpmn
        });
    }

    /// <summary>
    /// View process details with RACI matrix
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var process = await _context.Processes
            .Include(p => p.ProcessGroup)
                .ThenInclude(pg => pg!.Category)
            .Include(p => p.OwningUnit)
            .Include(p => p.Service)
            .Include(p => p.StrategicObjective)
            .Include(p => p.System)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Tasks.Where(t => !t.IsDeleted))
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.OwningUnit)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.RaciMatrix)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Tasks.Where(t => !t.IsDeleted))
                    .ThenInclude(t => t.RaciMatrix)
            .Include(p => p.RaciMatrix)
                .ThenInclude(r => r.OrganizationUnit)
            .Include(p => p.Risks.Where(r => r.IsActive))
            .Include(p => p.EnterpriseRisks.Where(r => r.IsActive))
            .Include(p => p.Measurements.Where(m => m.IsActive))
            .Include(p => p.ProcessServices)
                .ThenInclude(ps => ps.Service)
            .Include(p => p.ProcessStrategicObjectives)
                .ThenInclude(pso => pso.StrategicObjective)
            .Include(p => p.Assets.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (process == null)
            return NotFound();

        // SEC-001: record-level scope (IDOR). Block direct-URL access to a
        // process outside the caller's org scope. 404 not 403 — don't leak existence.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(process))
            return NotFound();

        _logger.LogInformation("Process {Code} has BPMN diagram: {HasBpmn}, Length: {Length}",
            process.Code,
            !string.IsNullOrWhiteSpace(process.BpmnDiagram),
            process.BpmnDiagram?.Length ?? 0);

        // Linked documents for the Details view
        ViewBag.LinkedDocuments = await _context.ProcessDocuments
            .Where(pd => pd.ProcessId == id)
            .Include(pd => pd.UserDocument)
            .Include(pd => pd.DocumentCategory)
            .Include(pd => pd.DocumentType)
            .OrderBy(pd => pd.DisplayOrder)
            .ToListAsync();

        // A "main" process is now identified structurally: another Process
        // points at it via ParentProcessId. (Old MP-/SP- prefix convention
        // was migrated into ParentProcessId by HierarchicalCodeMigration.)
        var isMainProcess = await _context.Processes
            .AnyAsync(p => p.ParentProcessId == process.Id && !p.IsDeleted);
        ViewBag.IsMainProcess = isMainProcess;

        if (isMainProcess)
        {
            var subProcesses = await _context.Processes
                .Include(p => p.OwningUnit)
                .Include(p => p.Activities.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.Tasks.Where(t => !t.IsDeleted))
                        .ThenInclude(t => t.RaciMatrix)
                .Include(p => p.Activities.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.RaciMatrix)
                .Include(p => p.RaciMatrix)
                .Include(p => p.Risks.Where(r => r.IsActive))
                .Include(p => p.EnterpriseRisks.Where(r => r.IsActive))
                .Include(p => p.Measurements.Where(m => m.IsActive))
                .Where(p => p.ParentProcessId == process.Id && !p.IsDeleted)
                .OrderBy(p => p.SortKey ?? p.Code)
                .ToListAsync();

            ViewBag.SubProcesses = subProcesses;

            // Aggregate stats from SubProcesses — RACI rollup spans all three
            // tiers (Process / Activity / Task) so the badge matches what
            // the /Processes/Raci/{id} editor actually manages.
            ViewBag.TotalActivities = subProcesses.Sum(sp => sp.Activities.Count);
            ViewBag.TotalTasks = subProcesses.Sum(sp => sp.Activities.Sum(a => a.Tasks.Count));
            ViewBag.TotalRaci = subProcesses.Sum(sp =>
                sp.RaciMatrix.Count
                + sp.Activities.Sum(a => a.RaciMatrix.Count + a.Tasks.Sum(t => t.RaciMatrix.Count)));
            ViewBag.TotalRisks = subProcesses.Sum(sp => sp.Risks.Count + sp.EnterpriseRisks.Count);
            ViewBag.TotalMeasurements = subProcesses.Sum(sp => sp.Measurements.Count);
        }

        // F-CC-002: surface change requests linked to this process. Recent 5
        // for the inline list, plus the total open count for the badge.
        ViewBag.RelatedChangeRequests = await _context.ChangeRequests
            .Where(cr => !cr.IsDeleted && cr.ProcessId == id)
            .OrderByDescending(cr => cr.CreatedAt)
            .Take(5)
            .ToListAsync();
        ViewBag.OpenChangeRequestCount = await _context.ChangeRequests
            .CountAsync(cr => !cr.IsDeleted && cr.ProcessId == id
                && cr.Status != ChangeRequestStatus.Implemented
                && cr.Status != ChangeRequestStatus.Rejected
                && cr.Status != ChangeRequestStatus.Cancelled);

        return View(process);
    }

    /// <summary>
    /// View RACI matrix for a process
    /// </summary>
    public async Task<IActionResult> Raci(string id)
    {
        var process = await _context.Processes
            .Include(p => p.ProcessGroup)
            .Include(p => p.OwningUnit)
            .Include(p => p.RaciMatrix)
                .ThenInclude(r => r.OrganizationUnit)
            .Include(p => p.RaciMatrix)
                .ThenInclude(r => r.JobPosition)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.OwningUnit)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.RaciMatrix)
                    .ThenInclude(r => r.OrganizationUnit)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.RaciMatrix)
                    .ThenInclude(r => r.JobPosition)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Tasks.Where(t => !t.IsDeleted))
                    .ThenInclude(t => t.OwningUnit)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Tasks.Where(t => !t.IsDeleted))
                    .ThenInclude(t => t.RaciMatrix)
                        .ThenInclude(r => r.OrganizationUnit)
            .Include(p => p.Activities.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Tasks.Where(t => !t.IsDeleted))
                    .ThenInclude(t => t.RaciMatrix)
                        .ThenInclude(r => r.JobPosition)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (process == null)
            return NotFound();

        // SEC-001: record-level scope (IDOR) on the RACI editor.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(process))
            return NotFound();

        // Editor needs the full active-org-unit list so every column is
        // available even when the process hasn't been populated yet.
        ViewBag.AllOrgUnits = await _context.OrganizationUnits
            .Where(u => !u.IsDeleted && u.IsActive)
            .OrderBy(u => u.DisplayOrder)
            .ToListAsync();

        // JobPosition catalog for the role-level RACI picker. Each entity has
        // an OwningUnit; the editor filters this catalog to roles scoped to
        // that unit + global roles (OrganizationUnitId == NULL). The JS does
        // the filter at Swal-open time so a single payload covers all 3
        // levels (Process / Activity / Task) on the same page.
        ViewBag.AllJobPositions = await _context.JobPositions
            .Where(j => !j.IsDeleted)
            .OrderBy(j => j.DisplayOrder)
            .ThenBy(j => j.NameEn)
            .ToListAsync();

        ViewBag.CanEdit = (User.HasClaim("Permission", "*.*") || User.HasClaim("Permission", "Process.*") || User.HasClaim("Permission", "Process.Edit"));

        return View(process);
    }

    /// <summary>
    /// Upsert a single RACI cell. Level ∈ {Process, Activity, Task}.
    /// Cell uniqueness is (entityId, unitId, jobRoleId) — a single org unit
    /// can host multiple RACI rows when each row is scoped to a different
    /// role (Director, Senior Specialist, Reviewer). jobRoleId is OPTIONAL:
    /// NULL = "the unit as a whole" (legacy/coarse), set = "the {role} role
    /// in the {unit}" (APQC/ISO 9001 §5.3 conformant).
    /// Role letter = "None" (or empty) clears the assignment.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> AssignRaci(string level, string entityId, string unitId, string? role, string? notes, string? jobRoleId = null)
    {
        if (string.IsNullOrEmpty(level) || string.IsNullOrEmpty(entityId) || string.IsNullOrEmpty(unitId))
            return BadRequest(new { success = false, error = "level, entityId and unitId are required." });

        // OrganizationUnitId is an int FK now; the cell posts it as a string.
        if (!int.TryParse(unitId, out var unitIdInt))
            return BadRequest(new { success = false, error = "unitId must be a valid organization unit id." });

        // Empty string from the client (no role picked) → NULL on the entity.
        // Treats "" and null identically so the editor never accidentally
        // creates a row with JobPositionId = "" (which would foreign-key-fail).
        var normalizedRoleId = string.IsNullOrWhiteSpace(jobRoleId) ? null : jobRoleId;

        // Parse incoming role ("R"/"A"/"C"/"I"/"None"/"").
        ESEMS.Web.Models.Enums.RACIRole? parsedRole = role switch
        {
            "R" or "Responsible"  => ESEMS.Web.Models.Enums.RACIRole.Responsible,
            "A" or "Accountable"  => ESEMS.Web.Models.Enums.RACIRole.Accountable,
            "C" or "Consulted"    => ESEMS.Web.Models.Enums.RACIRole.Consulted,
            "I" or "Informed"     => ESEMS.Web.Models.Enums.RACIRole.Informed,
            null or "" or "None"  => null,
            _ => null
        };

        // SEC-001/SEC-008: resolve the owning process for this RACI cell and
        // gate on scope before any mutation. Activity/Task levels carry their
        // own ids, so walk back to the owning ProcessId.
        var owningProcessId = level switch
        {
            "Process"  => entityId,
            "Activity" => await _context.Activities.Where(a => a.Id == entityId).Select(a => a.ProcessId).FirstOrDefaultAsync(),
            "Task"     => await _context.ProcessTasks.Where(t => t.Id == entityId).Select(t => t.Activity!.ProcessId).FirstOrDefaultAsync(),
            _ => null
        };
        if (string.IsNullOrEmpty(owningProcessId))
            return BadRequest(new { success = false, error = $"Unknown or invalid entity for level '{level}'." });
        var raciOwner = await _context.Processes.AsNoTracking()
            .Where(p => p.Id == owningProcessId && !p.IsDeleted)
            .Select(p => new { p.OwningUnitId })
            .FirstOrDefaultAsync();
        if (raciOwner == null)
            return NotFound();
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(new ProcessScopeProbe(raciOwner.OwningUnitId)))
            return NotFound();

        switch (level)
        {
            case "Process":
            {
                var existing = await _context.ProcessRacis
                    .FirstOrDefaultAsync(x => x.ProcessId == entityId && x.OrganizationUnitId == unitIdInt && x.JobPositionId == normalizedRoleId);
                if (parsedRole == null)
                {
                    if (existing != null) _context.ProcessRacis.Remove(existing);
                }
                else if (existing == null)
                {
                    _context.ProcessRacis.Add(new ESEMS.Web.Models.APQC.ProcessRaci
                    {
                        ProcessId = entityId, OrganizationUnitId = unitIdInt, JobPositionId = normalizedRoleId,
                        Role = parsedRole.Value, Notes = notes
                    });
                }
                else
                {
                    existing.Role = parsedRole.Value;
                    existing.Notes = notes;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                break;
            }
            case "Activity":
            {
                var existing = await _context.ActivityRacis
                    .FirstOrDefaultAsync(x => x.ActivityId == entityId && x.OrganizationUnitId == unitIdInt && x.JobPositionId == normalizedRoleId);
                if (parsedRole == null)
                {
                    if (existing != null) _context.ActivityRacis.Remove(existing);
                }
                else if (existing == null)
                {
                    _context.ActivityRacis.Add(new ESEMS.Web.Models.APQC.ActivityRaci
                    {
                        ActivityId = entityId, OrganizationUnitId = unitIdInt, JobPositionId = normalizedRoleId,
                        Role = parsedRole.Value, Notes = notes
                    });
                }
                else
                {
                    existing.Role = parsedRole.Value;
                    existing.Notes = notes;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                break;
            }
            case "Task":
            {
                var existing = await _context.TaskRacis
                    .FirstOrDefaultAsync(x => x.TaskId == entityId && x.OrganizationUnitId == unitIdInt && x.JobPositionId == normalizedRoleId);
                if (parsedRole == null)
                {
                    if (existing != null) _context.TaskRacis.Remove(existing);
                }
                else if (existing == null)
                {
                    _context.TaskRacis.Add(new ESEMS.Web.Models.APQC.TaskRaci
                    {
                        TaskId = entityId, OrganizationUnitId = unitIdInt, JobPositionId = normalizedRoleId,
                        Role = parsedRole.Value, Notes = notes
                    });
                }
                else
                {
                    existing.Role = parsedRole.Value;
                    existing.Notes = notes;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                break;
            }
            default:
                return BadRequest(new { success = false, error = $"Unknown level '{level}'." });
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true, role = parsedRole?.ToString() ?? "None" });
    }

    /// <summary>
    /// Update BPMN diagram for a process
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> UpdateBpmnDiagram(string id, [FromBody] UpdateBpmnRequest request)
    {
        try
        {
            var process = await _context.Processes.FindAsync(id);
            if (process == null)
                return NotFound(new { success = false, message = _localizer["Error_NotFound"].Value });

            // SEC-008: record-level scope (IDOR) on BPMN write.
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(process))
                return NotFound(new { success = false, message = _localizer["Error_NotFound"].Value });

            // Validate BPMN XML
            if (string.IsNullOrWhiteSpace(request.BpmnXml))
                return BadRequest(new { success = false, message = _localizer["BPMN_InvalidXML"].Value });

            // Check for required BPMN elements
            if (!request.BpmnXml.Contains("bpmn:definitions") ||
                !request.BpmnXml.Contains("xmlns:bpmn"))
                return BadRequest(new { success = false, message = _localizer["BPMN_InvalidXML"].Value });

            // Check XML is well-formed
            try
            {
                var xmlDoc = System.Xml.Linq.XDocument.Parse(request.BpmnXml);
            }
            catch (System.Xml.XmlException)
            {
                return BadRequest(new { success = false, message = _localizer["BPMN_InvalidXML"].Value });
            }

            // Create version record before updating
            await CreateBpmnVersionRecord(id, request.BpmnXml, request.ChangeDescription);

            process.BpmnDiagram = request.BpmnXml;
            process.UpdatedAt = DateTime.UtcNow;
            process.UpdatedById = User.Identity?.Name;

            await _context.SaveChangesAsync();

            _logger.LogInformation("BPMN diagram updated for process {Code} by {User}", process.Code, User.Identity?.Name);

            return Json(new { success = true, message = _localizer["BPMN_DiagramUpdated"].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating BPMN diagram for process {Id}", id);
            return StatusCode(500, new { success = false, message = _localizer["Error_ProcessingRequest"].Value });
        }
    }

    /// <summary>
    /// Create a BPMN version record
    /// </summary>
    private async Task CreateBpmnVersionRecord(string processId, string bpmnXml, string? changeDescription)
    {
        // Mark all existing versions as not current
        var existingVersions = await _context.ProcessBpmnVersions
            .Where(v => v.ProcessId == processId)
            .ToListAsync();

        foreach (var version in existingVersions)
        {
            version.IsCurrent = false;
        }

        // Get next version number
        var nextVersionNumber = existingVersions.Any()
            ? existingVersions.Max(v => v.VersionNumber) + 1
            : 1;

        // Get current user info
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userName = User.Identity?.Name ?? "System";

        // Create new version record
        var versionRecord = new Models.APQC.ProcessBpmnVersion
        {
            Id = Guid.NewGuid().ToString(),
            ProcessId = processId,
            VersionNumber = nextVersionNumber,
            BpmnXml = bpmnXml,
            ChangeDescription = changeDescription ?? $"Version {nextVersionNumber}",
            CreatedById = userId,
            CreatedByName = userName,
            CreatedAt = DateTime.UtcNow,
            IsCurrent = true,
            XmlSizeBytes = System.Text.Encoding.UTF8.GetByteCount(bpmnXml)
        };

        _context.ProcessBpmnVersions.Add(versionRecord);
    }

    /// <summary>
    /// Get BPMN version history for a process
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBpmnVersions(string processId)
    {
        try
        {
            // SEC-008: record-level scope (IDOR) — gate on the parent process.
            var owner = await _context.Processes.AsNoTracking()
                .Where(p => p.Id == processId && !p.IsDeleted)
                .Select(p => new { p.OwningUnitId })
                .FirstOrDefaultAsync();
            if (owner == null)
                return NotFound(new { success = false, error = _localizer["Error_NotFound"].Value });
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(new ProcessScopeProbe(owner.OwningUnitId)))
                return NotFound(new { success = false, error = _localizer["Error_NotFound"].Value });

            var versions = await _context.ProcessBpmnVersions
                .Where(v => v.ProcessId == processId)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new
                {
                    v.Id,
                    v.VersionNumber,
                    v.CreatedAt,
                    v.CreatedByName,
                    v.ChangeDescription,
                    v.IsCurrent,
                    FormattedSize = v.GetFormattedSize(),
                    Summary = v.GetVersionSummary()
                })
                .ToListAsync();

            return Json(new { success = true, versions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BPMN versions for process {ProcessId}", processId);
            return Json(new { success = false, error = _localizer["Error_ProcessingRequest"].Value });
        }
    }

    /// <summary>
    /// Get a specific BPMN version
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBpmnVersion(string versionId)
    {
        try
        {
            var version = await _context.ProcessBpmnVersions
                .Where(v => v.Id == versionId)
                .Select(v => new
                {
                    v.Id,
                    v.ProcessId,
                    v.VersionNumber,
                    v.BpmnXml,
                    v.CreatedAt,
                    v.CreatedByName,
                    v.ChangeDescription,
                    v.IsCurrent
                })
                .FirstOrDefaultAsync();

            if (version == null)
                return NotFound(new { success = false, error = _localizer["Error_NotFound"].Value });

            // SEC-008: record-level scope (IDOR) — gate on the version's owning process.
            var owner = await _context.Processes.AsNoTracking()
                .Where(p => p.Id == version.ProcessId && !p.IsDeleted)
                .Select(p => new { p.OwningUnitId })
                .FirstOrDefaultAsync();
            if (owner == null)
                return NotFound(new { success = false, error = _localizer["Error_NotFound"].Value });
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(new ProcessScopeProbe(owner.OwningUnitId)))
                return NotFound(new { success = false, error = _localizer["Error_NotFound"].Value });

            return Json(new { success = true, version });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BPMN version {VersionId}", versionId);
            return Json(new { success = false, error = _localizer["Error_ProcessingRequest"].Value });
        }
    }

    /// <summary>
    /// Restore a BPMN version
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> RestoreBpmnVersion(string versionId)
    {
        try
        {
            var version = await _context.ProcessBpmnVersions
                .Include(v => v.Process)
                .FirstOrDefaultAsync(v => v.Id == versionId);

            if (version == null || version.Process == null)
                return NotFound(new { success = false, error = _localizer["Error_NotFound"].Value });

            // SEC-008: record-level scope (IDOR) on BPMN restore.
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(version.Process))
                return NotFound(new { success = false, error = _localizer["Error_NotFound"].Value });

            // Create a new version record for the restore
            await CreateBpmnVersionRecord(
                version.ProcessId,
                version.BpmnXml,
                $"Restored from version {version.VersionNumber}");

            // Update the process with the restored BPMN
            version.Process.BpmnDiagram = version.BpmnXml;
            version.Process.UpdatedAt = DateTime.UtcNow;
            version.Process.UpdatedById = User.Identity?.Name;

            await _context.SaveChangesAsync();

            _logger.LogInformation("BPMN version {VersionId} restored for process {ProcessId} by {User}",
                versionId, version.ProcessId, User.Identity?.Name);

            return Json(new { success = true, message = _localizer["Success_VersionRestored"].Value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring BPMN version {VersionId}", versionId);
            return Json(new { success = false, error = _localizer["Error_ProcessingRequest"].Value });
        }
    }

    /// <summary>
    /// Create process form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    public async Task<IActionResult> Create(string? processGroupId = null, string? owningUnitId = null)
    {
        await PopulateDropdowns();
        var model = new Process();
        if (!string.IsNullOrEmpty(processGroupId))
            model.ProcessGroupId = processGroupId;
        if (!string.IsNullOrWhiteSpace(owningUnitId) && int.TryParse(owningUnitId, out var owningUnitIdInt))
        {
            model.OwningUnitId = owningUnitIdInt;
            // Fetch owning-unit display name so the form can show the locked unit
            var unit = await _context.OrganizationUnits
                .FirstOrDefaultAsync(o => o.Id == owningUnitIdInt);
            ViewBag.LockedOwningUnitId = owningUnitId;
            ViewBag.LockedOwningUnitName = unit == null ? null
                : (System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? unit.NameAr : unit.NameEn);
        }
        ViewBag.ResponsibilityPicker = new Models.ViewModels.ResponsibilityPickerVM
        {
            FieldName = "SelectedResponsibilityIds",
            PreferredUnitId = model.OwningUnitId?.ToString(),
            Selected = new List<OrganizationUnitResponsibility>()
        };
        return View(model);
    }

    /// <summary>
    /// Create process
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Process process, List<string>? SelectedServiceIds,
        List<string>? SelectedAssetIds, List<string>? SelectedRiskIds,
        List<string>? SelectedObjectiveIds, List<string>? SelectedResponsibilityIds,
        string? TagList, string? ActivitiesJson,
        string? DocumentLinksJson)
    {
        // Process Code is system-generated under the parent ProcessGroup —
        // strip any stray posted value and clear the [Required] error.
        process.Code = string.Empty;
        ModelState.Remove(nameof(process.Code));

        // DisplayOrder is hidden too: append to the end of the ProcessGroup.
        if (process.DisplayOrder == 0)
        {
            process.DisplayOrder = await GetNextProcessDisplayOrderAsync(process.ProcessGroupId);
        }

        if (ModelState.IsValid)
        {
            process.Id = Guid.NewGuid().ToString();
            // Allocate the next X.Y.Z code under the chosen ProcessGroup,
            // retrying on the rare unique-constraint race.
            await _codeSvc.AllocateWithRetryAsync(async () =>
            {
                process.Code = await GenerateNextProcessCodeAsync(process.ProcessGroupId);
                process.SortKey = HierarchicalCodeService.SortKeyFor(process.Code);
                return true;
            });

            process.CreatedAt = DateTime.UtcNow;
            process.UpdatedAt = DateTime.UtcNow;
            process.CreatedById = User.Identity?.Name;
            process.Status = ProcessStatus.Draft;

            // Set tags from tag input
            if (!string.IsNullOrWhiteSpace(TagList))
            {
                process.Tags = TagList;
            }

            _context.Processes.Add(process);

            // Add ProcessService relationships for selected services.
            // Per-link Criticality/IsMandatory default to 3 (Medium) / true at
            // create-time — users tune them per row on Process/Service Details.
            if (SelectedServiceIds != null && SelectedServiceIds.Any())
            {
                foreach (var serviceId in SelectedServiceIds)
                {
                    _context.ProcessServices.Add(new ProcessService
                    {
                        ProcessId = process.Id,
                        ServiceId = serviceId,
                        Criticality = 3,
                        IsMandatory = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // Link selected assets to this process
            if (SelectedAssetIds != null && SelectedAssetIds.Any())
            {
                var assets = await _context.Assets
                    .Where(a => SelectedAssetIds.Contains(a.Id) && !a.IsDeleted)
                    .ToListAsync();
                foreach (var asset in assets)
                {
                    asset.ProcessId = process.Id;
                    asset.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Link selected risks to this process
            if (SelectedRiskIds != null && SelectedRiskIds.Any())
            {
                var risks = await _context.EnterpriseRisks
                    .Where(r => SelectedRiskIds.Contains(r.Id) && !r.IsDeleted)
                    .ToListAsync();
                foreach (var risk in risks)
                {
                    risk.ProcessId = process.Id;
                    risk.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Add ProcessStrategicObjective relationships
            if (SelectedObjectiveIds != null && SelectedObjectiveIds.Any())
            {
                foreach (var objectiveId in SelectedObjectiveIds)
                {
                    _context.ProcessStrategicObjectives.Add(new ProcessStrategicObjective
                    {
                        ProcessId = process.Id,
                        StrategicObjectiveId = objectiveId,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // Add ProcessResponsibility relationships — chartered OrganizationUnit
            // responsibilities this process fulfills.
            if (SelectedResponsibilityIds != null && SelectedResponsibilityIds.Any())
            {
                foreach (var rId in SelectedResponsibilityIds.Distinct())
                {
                    _context.ProcessResponsibilities.Add(new ProcessResponsibility
                    {
                        ProcessId = process.Id,
                        ResponsibilityId = rId,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // Document Linking: each row references a UserDocument in My Space
            // (uploaded either directly from the computer or picked from the
            // user's existing library) with its own category/type/language.
            PersistDocumentLinks(process.Id, DocumentLinksJson, replaceExisting: false);

            // Activities step of the Create wizard (optional). Client serializes
            // rows into ActivitiesJson as [{ code, nameEn, nameAr }].
            //
            // Audit fix: previously rows like { nameEn: "1", nameAr: "1" }
            // were silently saved even though the Activity Edit form rejects
            // them via BilingualEntity's [MinLength(3)] data annotation. Now
            // each row gets the same ≥3-character gate at create time, so
            // the user fixes invalid data before save rather than after.
            // Rows with one side >= 3 chars and the other side empty/short
            // get the long side mirrored into the short side as a kind
            // fallback (matches how the Excel importer handles half-filled
            // bilingual columns).
            if (!string.IsNullOrWhiteSpace(ActivitiesJson))
            {
                try
                {
                    var rows = System.Text.Json.JsonSerializer.Deserialize<List<SetupActivityRow>>(ActivitiesJson,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<SetupActivityRow>();
                    var order = 0;
                    var rejectedActivityRows = new List<string>();
                    foreach (var r in rows)
                    {
                        var enRaw = (r.NameEn ?? string.Empty).Trim();
                        var arRaw = (r.NameAr ?? string.Empty).Trim();
                        if (enRaw.Length == 0 && arRaw.Length == 0) continue;

                        // Validation parity with Activity Edit: NameEn and
                        // NameAr each need >= 3 chars. Use one side to seed
                        // the other if the operator only filled one.
                        if (enRaw.Length == 0 && arRaw.Length >= 3) enRaw = arRaw;
                        else if (arRaw.Length == 0 && enRaw.Length >= 3) arRaw = enRaw;

                        if (enRaw.Length < 3 || arRaw.Length < 3)
                        {
                            rejectedActivityRows.Add($"{enRaw}/{arRaw}");
                            continue;
                        }

                        order++;
                        // Activity.Code is globally unique. Auto-generate using the APQC-style
                        // hierarchical convention: {ProcessCode}.01, {ProcessCode}.02, ...
                        // Zero-padded so codes sort lexicographically.
                        var code = $"{process.Code}.{order:D2}";
                        _context.Activities.Add(new Models.APQC.Activity
                        {
                            ProcessId = process.Id,
                            Code = code,
                            NameEn = enRaw,
                            NameAr = arRaw,
                            DisplayOrder = order,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            CreatedById = User.Identity?.Name
                        });
                    }
                    if (rejectedActivityRows.Count > 0)
                    {
                        _logger.LogWarning("Process {Code} create wizard: rejected {N} activity rows under 3 chars: {Rows}",
                            process.Code, rejectedActivityRows.Count, string.Join(" | ", rejectedActivityRows));
                        TempData["ActivityRowsRejected"] = rejectedActivityRows.Count;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse wizard ActivitiesJson for new process {ProcessId}", process.Id);
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_ProcessCreated"].Value;
            TempData["JustCreated"] = "1";
            // Step 4 of the Create wizard lets the user finish straight into the
            // BPMN editor. When that option is chosen the form posts
            // ContinueToBpmn = "1".
            if (Request.Form.TryGetValue("ContinueToBpmn", out var ctb) && ctb == "1")
            {
                return RedirectToAction("Diagrams", "AI", new { processId = process.Id });
            }
            return RedirectToAction(nameof(Details), new { id = process.Id });
        }

        await PopulateDropdowns();
        return View(process);
    }

    /// <summary>
    /// Edit process form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var process = await _context.Processes.FindAsync(id);
        if (process == null || process.IsDeleted)
            return NotFound();

        // SEC-001: record-level scope (IDOR) on the Edit form.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(process))
            return NotFound();

        // #14: only full-access (Unscoped/All) users may reassign the owning unit.
        ViewBag.CanReassignUnit = scope.IsUnscoped;

        await PopulateDropdowns();

        // Load existing linked services (M:M via ProcessServices)
        ViewBag.ExistingServiceIds = await _context.ProcessServices
            .Where(ps => ps.ProcessId == id && ps.IsActive)
            .Select(ps => ps.ServiceId)
            .ToListAsync();

        // Load existing strategic objectives (M:M via ProcessStrategicObjectives)
        ViewBag.ExistingObjectiveIds = await _context.ProcessStrategicObjectives
            .Where(pso => pso.ProcessId == id && pso.IsActive)
            .Select(pso => pso.StrategicObjectiveId)
            .ToListAsync();

        // Load existing responsibility links — pre-populates the picker so
        // the chips render server-side without an extra round-trip.
        var existingResponsibilities = await _context.ProcessResponsibilities
            .Where(pr => pr.ProcessId == id && pr.IsActive)
            .Include(pr => pr.Responsibility!).ThenInclude(r => r.OrganizationUnit)
            .Select(pr => pr.Responsibility!)
            .ToListAsync();
        ViewBag.ResponsibilityPicker = new Models.ViewModels.ResponsibilityPickerVM
        {
            FieldName = "SelectedResponsibilityIds",
            Selected = existingResponsibilities,
            PreferredUnitId = process.OwningUnitId?.ToString()
        };

        // Load existing linked assets (1:M via Asset.ProcessId)
        ViewBag.ExistingAssetIds = await _context.Assets
            .Where(a => a.ProcessId == id && !a.IsDeleted)
            .Select(a => a.Id)
            .ToListAsync();

        // Load existing linked enterprise risks (1:M via EnterpriseRisk.ProcessId)
        ViewBag.ExistingRiskIds = await _context.EnterpriseRisks
            .Where(r => r.ProcessId == id && !r.IsDeleted)
            .Select(r => r.Id)
            .ToListAsync();

        // Load existing linked documents for the Document Linking partial
        ViewBag.ExistingProcessDocuments = await LoadExistingProcessDocumentsAsync(id);

        return View(process);
    }

    /// <summary>
    /// Projects ProcessDocuments into the shape expected by the
    /// _ProcessDocumentLinking partial (anonymous, camelCase on the wire).
    /// </summary>
    private async Task<List<object>> LoadExistingProcessDocumentsAsync(string processId)
    {
        var links = await _context.ProcessDocuments
            .Where(pd => pd.ProcessId == processId)
            .Include(pd => pd.UserDocument)
            .OrderBy(pd => pd.DisplayOrder)
            .ToListAsync();

        return links.Select(pd => (object)new
        {
            id = pd.Id,
            userDocumentId = pd.UserDocumentId,
            originalName = pd.UserDocument?.OriginalName ?? "(file)",
            fileSize = pd.UserDocument?.FileSize ?? 0,
            url = pd.UserDocument != null
                ? $"/uploads/myspace/{pd.UserDocument.UserId}/{pd.UserDocument.FileName}"
                : null,
            documentCategoryId = pd.DocumentCategoryId,
            documentTypeId = pd.DocumentTypeId,
            documentLanguage = pd.DocumentLanguage
        }).ToList();
    }

    /// <summary>
    /// Edit process
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Process process,
        List<string>? SelectedServiceIds, List<string>? SelectedObjectiveIds,
        List<string>? SelectedAssetIds, List<string>? SelectedRiskIds,
        List<string>? SelectedResponsibilityIds,
        string? TagList, string? DocumentLinksJson)
    {
        if (id != process.Id)
            return NotFound();

        // SEC-001/SEC-002: load the persisted entity, gate on its real
        // OwningUnitId (IDOR), then load-then-patch only user-editable fields.
        // The bound `process` is untrusted — never _context.Update() it, or a
        // crafted post could overwrite OwningUnitId / IsDeleted / Code / audit
        // fields. Code/DisplayOrder are system-managed too.
        ModelState.Remove(nameof(Process.Code));
        ModelState.Remove(nameof(Process.DisplayOrder));

        var existing = await _context.Processes
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (existing == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(existing))
            return NotFound();

        // #14: scoped users can't change the owning unit — drop it from
        // validation so a disabled (non-posted) dropdown doesn't fail ModelState.
        if (!scope.IsUnscoped)
            ModelState.Remove(nameof(Process.OwningUnitId));

        if (ModelState.IsValid)
        {
            // TagList from chip UI overrides the bound Tags input when posted.
            var newTags = TagList != null
                ? (string.IsNullOrWhiteSpace(TagList) ? null : TagList)
                : process.Tags;

            // Patch ONLY user-editable fields. Code/DisplayOrder stay whatever
            // the Create flow assigned; Id/CreatedAt/CreatedById/IsDeleted/
            // DeletedAt are never bound.
            // #14: OwningUnitId is reassignable ONLY by full-access (Unscoped)
            // users; for scoped users it stays as persisted (no scope-escape).
            if (scope.IsUnscoped && process.OwningUnitId != null)
                existing.OwningUnitId = process.OwningUnitId;
            existing.ProcessGroupId       = process.ProcessGroupId;
            existing.NameEn               = process.NameEn;
            existing.NameAr               = process.NameAr;
            existing.DescriptionEn        = process.DescriptionEn;
            existing.DescriptionAr        = process.DescriptionAr;
            existing.ProcessType          = process.ProcessType;
            existing.Status               = process.Status;
            existing.SystemId             = process.SystemId;
            existing.Tags                 = newTags;
            existing.EstimatedDuration    = process.EstimatedDuration;
            existing.DurationUnit         = process.DurationUnit;
            existing.EstimatedCost        = process.EstimatedCost;
            existing.AutomationStatus     = process.AutomationStatus;
            existing.HasDetailedBreakdown = process.HasDetailedBreakdown;
            existing.UpdatedAt            = DateTime.UtcNow;
            existing.UpdatedById          = User.Identity?.Name;
            existing.Version++;

            // Update Document Linking: wipe and replace (simplest correct
            // behaviour — the posted JSON is the new full state).
            PersistDocumentLinks(process.Id, DocumentLinksJson, replaceExisting: true);

            // === Services (M:M ProcessServices) — wipe & replace ===
            var existingServiceLinks = await _context.ProcessServices
                .Where(ps => ps.ProcessId == id)
                .ToListAsync();
            _context.ProcessServices.RemoveRange(existingServiceLinks);

            if (SelectedServiceIds != null && SelectedServiceIds.Any())
            {
                foreach (var serviceId in SelectedServiceIds)
                {
                    _context.ProcessServices.Add(new ProcessService
                    {
                        ProcessId = process.Id,
                        ServiceId = serviceId,
                        Criticality = 3,
                        IsMandatory = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // === Strategic Objectives (M:M ProcessStrategicObjectives) — wipe & replace ===
            var existingObjectiveLinks = await _context.ProcessStrategicObjectives
                .Where(pso => pso.ProcessId == id)
                .ToListAsync();
            _context.ProcessStrategicObjectives.RemoveRange(existingObjectiveLinks);

            if (SelectedObjectiveIds != null && SelectedObjectiveIds.Any())
            {
                foreach (var objectiveId in SelectedObjectiveIds)
                {
                    _context.ProcessStrategicObjectives.Add(new ProcessStrategicObjective
                    {
                        ProcessId = process.Id,
                        StrategicObjectiveId = objectiveId,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // === Responsibilities (M:M ProcessResponsibilities) — wipe & replace ===
            var existingRespLinks = await _context.ProcessResponsibilities
                .Where(pr => pr.ProcessId == id)
                .ToListAsync();
            _context.ProcessResponsibilities.RemoveRange(existingRespLinks);

            if (SelectedResponsibilityIds != null && SelectedResponsibilityIds.Any())
            {
                foreach (var rId in SelectedResponsibilityIds.Distinct())
                {
                    _context.ProcessResponsibilities.Add(new ProcessResponsibility
                    {
                        ProcessId = process.Id,
                        ResponsibilityId = rId,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // === Assets (1:M Asset.ProcessId) — detach removed, attach new ===
            var currentAssetLinks = await _context.Assets
                .Where(a => a.ProcessId == id && !a.IsDeleted)
                .ToListAsync();
            var keepAssetIds = SelectedAssetIds ?? new List<string>();
            foreach (var asset in currentAssetLinks.Where(a => !keepAssetIds.Contains(a.Id)))
            {
                asset.ProcessId = null;
                asset.UpdatedAt = DateTime.UtcNow;
            }
            var addAssetIds = keepAssetIds.Except(currentAssetLinks.Select(a => a.Id)).ToList();
            if (addAssetIds.Any())
            {
                var assetsToAttach = await _context.Assets
                    .Where(a => addAssetIds.Contains(a.Id) && !a.IsDeleted)
                    .ToListAsync();
                foreach (var asset in assetsToAttach)
                {
                    asset.ProcessId = process.Id;
                    asset.UpdatedAt = DateTime.UtcNow;
                }
            }

            // === Enterprise Risks (1:M EnterpriseRisk.ProcessId) — detach removed, attach new ===
            var currentRiskLinks = await _context.EnterpriseRisks
                .Where(r => r.ProcessId == id && !r.IsDeleted)
                .ToListAsync();
            var keepRiskIds = SelectedRiskIds ?? new List<string>();
            foreach (var risk in currentRiskLinks.Where(r => !keepRiskIds.Contains(r.Id)))
            {
                risk.ProcessId = null;
                risk.UpdatedAt = DateTime.UtcNow;
            }
            var addRiskIds = keepRiskIds.Except(currentRiskLinks.Select(r => r.Id)).ToList();
            if (addRiskIds.Any())
            {
                var risksToAttach = await _context.EnterpriseRisks
                    .Where(r => addRiskIds.Contains(r.Id) && !r.IsDeleted)
                    .ToListAsync();
                foreach (var risk in risksToAttach)
                {
                    risk.ProcessId = process.Id;
                    risk.UpdatedAt = DateTime.UtcNow;
                }
            }

            try
            {
                await _context.SaveChangesAsync();

                TempData["Success"] = _localizer["Success_ProcessUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id = process.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                // DATA-009 — another user updated this process between our
                // load and our save. Detect deletion vs. concurrent edit,
                // surface a localized message so the user reloads and
                // re-applies their changes instead of silently last-write-
                // winning. Pairs with the same fix in ServicesController.
                if (!await _context.Processes.AnyAsync(p => p.Id == process.Id))
                    return NotFound();
                ModelState.AddModelError(string.Empty,
                    System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar")
                        ? "تم تعديل هذا السجل من قبل مستخدم آخر. يرجى تحديث الصفحة وإعادة المحاولة."
                        : "This record was modified by another user. Please reload the page and try again.");
            }
        }

        await PopulateDropdowns();

        // Reload existing links on validation failure
        ViewBag.ExistingServiceIds = await _context.ProcessServices
            .Where(ps => ps.ProcessId == id && ps.IsActive)
            .Select(ps => ps.ServiceId)
            .ToListAsync();
        ViewBag.ExistingObjectiveIds = await _context.ProcessStrategicObjectives
            .Where(pso => pso.ProcessId == id && pso.IsActive)
            .Select(pso => pso.StrategicObjectiveId)
            .ToListAsync();
        ViewBag.ExistingAssetIds = await _context.Assets
            .Where(a => a.ProcessId == id && !a.IsDeleted)
            .Select(a => a.Id)
            .ToListAsync();
        ViewBag.ExistingRiskIds = await _context.EnterpriseRisks
            .Where(r => r.ProcessId == id && !r.IsDeleted)
            .Select(r => r.Id)
            .ToListAsync();
        ViewBag.ExistingProcessDocuments = await LoadExistingProcessDocumentsAsync(id);

        return View(process);
    }

    #region Process-Service Linking API

    /// <summary>
    /// Link a service to this process
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> LinkService(string processId, string serviceId, int criticality = 3, bool isMandatory = true, string? notes = null)
    {
        try
        {
            var process = await _context.Processes.FindAsync(processId);
            if (process == null || process.IsDeleted)
                return Json(new { success = false, message = "Process not found." });

            // SEC-008: record-level scope (IDOR) on the parent process.
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(process))
                return Json(new { success = false, message = "Process not found." });

            var service = await _context.Services.FindAsync(serviceId);
            if (service == null || service.IsDeleted)
                return Json(new { success = false, message = "Service not found." });

            var existing = await _context.ProcessServices
                .FirstOrDefaultAsync(ps => ps.ProcessId == processId && ps.ServiceId == serviceId);

            if (existing != null)
                return Json(new { success = false, message = "This service is already linked to this process." });

            var processService = new ProcessService
            {
                ProcessId = processId,
                ServiceId = serviceId,
                Criticality = criticality,
                IsMandatory = isMandatory,
                Notes = notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedById = User.Identity?.Name
            };

            _context.ProcessServices.Add(processService);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Service {ServiceId} linked to Process {ProcessId} by {User}", serviceId, processId, User.Identity?.Name);

            return Json(new { success = true, message = "Service linked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking service {ServiceId} to process {ProcessId}", serviceId, processId);
            return Json(new { success = false, message = "An error occurred while linking the service." });
        }
    }

    /// <summary>
    /// Unlink a service from this process
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> UnlinkService(string processId, string serviceId)
    {
        try
        {
            // SEC-008: record-level scope (IDOR) on the parent process.
            var owner = await _context.Processes.AsNoTracking()
                .Where(p => p.Id == processId && !p.IsDeleted)
                .Select(p => new { p.OwningUnitId })
                .FirstOrDefaultAsync();
            if (owner == null)
                return Json(new { success = false, message = "Process not found." });
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(new ProcessScopeProbe(owner.OwningUnitId)))
                return Json(new { success = false, message = "Process not found." });

            var ps = await _context.ProcessServices
                .FirstOrDefaultAsync(ps => ps.ProcessId == processId && ps.ServiceId == serviceId);

            if (ps == null)
                return Json(new { success = false, message = "Service relationship not found." });

            _context.ProcessServices.Remove(ps);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Service {ServiceId} unlinked from Process {ProcessId} by {User}", serviceId, processId, User.Identity?.Name);

            return Json(new { success = true, message = "Service unlinked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking service {ServiceId} from process {ProcessId}", serviceId, processId);
            return Json(new { success = false, message = "An error occurred while unlinking the service." });
        }
    }

    /// <summary>
    /// Update process-service relationship details
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> UpdateProcessService(string processId, string serviceId, int criticality, bool isMandatory, string? notes = null)
    {
        try
        {
            // SEC-008: record-level scope (IDOR) on the parent process.
            var owner = await _context.Processes.AsNoTracking()
                .Where(p => p.Id == processId && !p.IsDeleted)
                .Select(p => new { p.OwningUnitId })
                .FirstOrDefaultAsync();
            if (owner == null)
                return Json(new { success = false, message = "Relationship not found." });
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(new ProcessScopeProbe(owner.OwningUnitId)))
                return Json(new { success = false, message = "Relationship not found." });

            var ps = await _context.ProcessServices
                .FirstOrDefaultAsync(ps => ps.ProcessId == processId && ps.ServiceId == serviceId);

            if (ps == null)
                return Json(new { success = false, message = "Relationship not found." });

            ps.Criticality = criticality;
            ps.IsMandatory = isMandatory;
            ps.Notes = notes;
            ps.UpdatedAt = DateTime.UtcNow;
            ps.UpdatedById = User.Identity?.Name;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Process-Service relationship updated for Process {ProcessId} and Service {ServiceId} by {User}", processId, serviceId, User.Identity?.Name);

            return Json(new { success = true, message = "Relationship updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating process-service relationship for Process {ProcessId} and Service {ServiceId}", processId, serviceId);
            return Json(new { success = false, message = "An error occurred while updating the relationship." });
        }
    }

    /// <summary>
    /// Get available services for linking (not already linked to this process)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableServices(string processId)
    {
        try
        {
            // SEC-008: record-level scope (IDOR) on the parent process.
            var owner = await _context.Processes.AsNoTracking()
                .Where(p => p.Id == processId && !p.IsDeleted)
                .Select(p => new { p.OwningUnitId })
                .FirstOrDefaultAsync();
            if (owner == null)
                return Json(new List<object>());
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(new ProcessScopeProbe(owner.OwningUnitId)))
                return Json(new List<object>());

            var linkedServiceIds = await _context.ProcessServices
                .Where(ps => ps.ProcessId == processId)
                .Select(ps => ps.ServiceId)
                .ToListAsync();

            var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            var availableServices = await _context.Services
                .Where(s => !s.IsDeleted && !linkedServiceIds.Contains(s.Id))
                .ApplyOwningUnitScope(scope)
                .OrderBy(s => s.NameEn)
                .Select(s => new
                {
                    s.Id,
                    s.NameEn,
                    s.NameAr,
                    Name = isArabic ? s.NameAr : s.NameEn
                })
                .ToListAsync();

            return Json(availableServices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available services for process {ProcessId}", processId);
            return Json(new List<object>());
        }
    }

    #endregion

    #region Post-create Setup Wizard

    /// <summary>
    /// 4-step setup wizard that guides a newly-created process through:
    ///   1. Link Risks
    ///   2. Link Services
    ///   3. Activities
    ///   4. BPMN Diagram
    /// Each step is standalone — the user can Back / Skip / Next / Finish.
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> Setup(string id, int step = 1)
    {
        if (step < 1) step = 1;
        if (step > 4) step = 4;

        var process = await _context.Processes
            .Include(p => p.ProcessGroup)
            .Include(p => p.OwningUnit)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (process == null) return NotFound();

        // SEC-001: record-level scope (IDOR) on the Setup wizard.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(process))
            return NotFound();

        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");

        // Lookups for current step
        if (step == 1)
        {
            var risks = await _context.EnterpriseRisks
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.RiskNumber)
                .Select(r => new { r.Id, r.RiskNumber, Name = isArabic ? r.NameAr : r.NameEn, r.ProcessId })
                .ToListAsync();
            ViewBag.AllRisks = risks;
            ViewBag.LinkedRiskIds = risks.Where(r => r.ProcessId == id).Select(r => r.Id).ToList();
        }
        else if (step == 2)
        {
            var services = await _context.Services
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.NameEn)
                .Select(s => new { s.Id, s.Code, Name = isArabic ? s.NameAr : s.NameEn })
                .ToListAsync();
            ViewBag.AllServices = services;
            ViewBag.LinkedServiceIds = await _context.ProcessServices
                .Where(ps => ps.ProcessId == id && ps.IsActive)
                .Select(ps => ps.ServiceId)
                .ToListAsync();
        }
        else if (step == 3)
        {
            ViewBag.ExistingActivities = await _context.Activities
                .Where(a => a.ProcessId == id && !a.IsDeleted)
                .OrderBy(a => a.DisplayOrder)
                .Select(a => new { a.Id, a.Code, a.NameEn, a.NameAr, a.DisplayOrder })
                .ToListAsync();
        }

        ViewBag.CurrentStep = step;
        ViewBag.ProcessId = id;
        return View("Setup", process);
    }

    /// <summary>
    /// Save the current setup step and navigate. action = "next" | "back" | "skip" | "finish".
    /// Only "next" and "finish" persist the posted data; "skip" and "back" discard it.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetupSave(
        string id,
        int step,
        string action,
        List<string>? SelectedRiskIds,
        List<string>? SelectedServiceIds,
        string? ActivitiesJson)
    {
        var process = await _context.Processes.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (process == null) return NotFound();

        // SEC-001: record-level scope (IDOR) on the Setup wizard save.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(process))
            return NotFound();

        var shouldSave = action == "next" || action == "finish";

        if (shouldSave)
        {
            if (step == 1)
            {
                // Link enterprise risks to this process. Unlink ones that were removed
                // (only risks previously attached to this process are candidates for removal).
                var current = await _context.EnterpriseRisks
                    .Where(r => !r.IsDeleted && r.ProcessId == id)
                    .ToListAsync();
                var selected = SelectedRiskIds ?? new List<string>();
                foreach (var r in current.Where(r => !selected.Contains(r.Id)))
                {
                    r.ProcessId = null;
                    r.UpdatedAt = DateTime.UtcNow;
                }
                if (selected.Any())
                {
                    var toLink = await _context.EnterpriseRisks
                        .Where(r => !r.IsDeleted && selected.Contains(r.Id))
                        .ToListAsync();
                    foreach (var r in toLink)
                    {
                        r.ProcessId = id;
                        r.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
            else if (step == 2)
            {
                var selected = SelectedServiceIds ?? new List<string>();
                var existing = await _context.ProcessServices
                    .Where(ps => ps.ProcessId == id)
                    .ToListAsync();

                // Deactivate removed
                foreach (var ps in existing.Where(ps => !selected.Contains(ps.ServiceId)))
                {
                    ps.IsActive = false;
                    ps.UpdatedAt = DateTime.UtcNow;
                }
                // Reactivate or add selected
                foreach (var svcId in selected)
                {
                    var row = existing.FirstOrDefault(ps => ps.ServiceId == svcId);
                    if (row == null)
                    {
                        _context.ProcessServices.Add(new ProcessService
                        {
                            ProcessId = id,
                            ServiceId = svcId,
                            Criticality = 3,
                            IsMandatory = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            CreatedById = User.Identity?.Name,
                            IsActive = true
                        });
                    }
                    else if (!row.IsActive)
                    {
                        row.IsActive = true;
                        row.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
            else if (step == 3 && !string.IsNullOrWhiteSpace(ActivitiesJson))
            {
                try
                {
                    var rows = System.Text.Json.JsonSerializer.Deserialize<List<SetupActivityRow>>(ActivitiesJson,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<SetupActivityRow>();

                    var existing = await _context.Activities
                        .Where(a => a.ProcessId == id && !a.IsDeleted)
                        .ToListAsync();

                    var incomingIds = rows.Where(r => !string.IsNullOrWhiteSpace(r.Id)).Select(r => r.Id!).ToHashSet();
                    // Soft-delete ones not in the incoming list
                    foreach (var a in existing.Where(a => !incomingIds.Contains(a.Id)))
                    {
                        a.IsDeleted = true;
                        a.DeletedAt = DateTime.UtcNow;
                        a.UpdatedAt = DateTime.UtcNow;
                    }

                    var order = 0;
                    foreach (var r in rows)
                    {
                        order++;
                        if (string.IsNullOrWhiteSpace(r.NameEn) && string.IsNullOrWhiteSpace(r.NameAr)) continue;
                        var activity = existing.FirstOrDefault(a => a.Id == r.Id);
                        if (activity == null)
                        {
                            _context.Activities.Add(new Models.APQC.Activity
                            {
                                ProcessId = id,
                                Code = string.IsNullOrWhiteSpace(r.Code) ? $"{process.Code}.{order}" : r.Code!,
                                NameEn = r.NameEn ?? string.Empty,
                                NameAr = r.NameAr ?? string.Empty,
                                DisplayOrder = order,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                CreatedById = User.Identity?.Name
                            });
                        }
                        else
                        {
                            activity.Code = string.IsNullOrWhiteSpace(r.Code) ? activity.Code : r.Code!;
                            activity.NameEn = r.NameEn ?? activity.NameEn;
                            activity.NameAr = r.NameAr ?? activity.NameAr;
                            activity.DisplayOrder = order;
                            activity.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse wizard activities payload for process {ProcessId}", id);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = _localizer["Success_ProcessUpdated"].Value;
        }

        // Navigate
        if (action == "finish")
            return RedirectToAction(nameof(Details), new { id });
        if (action == "back")
            return RedirectToAction(nameof(Setup), new { id, step = Math.Max(1, step - 1) });
        // next or skip
        var targetStep = step + 1;
        if (targetStep > 4)
            return RedirectToAction(nameof(Details), new { id });
        return RedirectToAction(nameof(Setup), new { id, step = targetStep });
    }

    /// <summary>
    /// DTO used to deserialize the wizard Activities grid payload.
    /// </summary>
    public class SetupActivityRow
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? NameEn { get; set; }
        public string? NameAr { get; set; }
    }

    /// <summary>
    /// DTO matching the JSON rows posted by the Document Linking UI.
    /// </summary>
    private class DocumentLinkRow
    {
        public string? UserDocumentId { get; set; }
        public string? DocumentCategoryId { get; set; }
        public string? DocumentTypeId { get; set; }
        public string? DocumentLanguage { get; set; }
    }

    /// <summary>
    /// Parses the DocumentLinksJson payload and syncs ProcessDocuments for a
    /// given process. When <paramref name="replaceExisting"/> is true, all
    /// current links are removed before the posted rows are inserted.
    /// Silently ignores rows referencing unknown or foreign UserDocuments.
    /// </summary>
    private void PersistDocumentLinks(string processId, string? documentLinksJson, bool replaceExisting)
    {
        List<DocumentLinkRow>? rows = null;
        if (!string.IsNullOrWhiteSpace(documentLinksJson))
        {
            try
            {
                rows = System.Text.Json.JsonSerializer.Deserialize<List<DocumentLinkRow>>(
                    documentLinksJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse DocumentLinksJson for process {ProcessId}", processId);
                rows = null;
            }
        }

        if (replaceExisting)
        {
            var existing = _context.ProcessDocuments.Where(pd => pd.ProcessId == processId).ToList();
            if (existing.Count > 0)
                _context.ProcessDocuments.RemoveRange(existing);
        }

        if (rows == null || rows.Count == 0) return;

        // Only accept rows pointing at UserDocuments that actually exist and
        // are not soft-deleted. (Defence-in-depth: the UI can't submit foreign
        // ids in normal use, but we don't want a crafted form to create
        // dangling references.)
        var referencedIds = rows
            .Select(r => r.UserDocumentId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var validIds = _context.UserDocuments
            .Where(d => referencedIds.Contains(d.Id) && !d.IsDeleted)
            .Select(d => d.Id)
            .ToHashSet();

        var createdBy = User.Identity?.Name;
        var order = 0;
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.UserDocumentId)) continue;
            if (!validIds.Contains(r.UserDocumentId!)) continue;

            order++;
            _context.ProcessDocuments.Add(new ProcessDocument
            {
                Id = Guid.NewGuid().ToString(),
                ProcessId = processId,
                UserDocumentId = r.UserDocumentId!,
                DocumentCategoryId = string.IsNullOrWhiteSpace(r.DocumentCategoryId) ? null : r.DocumentCategoryId,
                DocumentTypeId = string.IsNullOrWhiteSpace(r.DocumentTypeId) ? null : r.DocumentTypeId,
                DocumentLanguage = string.IsNullOrWhiteSpace(r.DocumentLanguage) ? null : r.DocumentLanguage,
                DisplayOrder = order,
                CreatedAt = DateTime.UtcNow,
                CreatedById = createdBy
            });
        }
    }

    #endregion

    /// <summary>
    /// Soft-deletes a process when no active dependents exist. Audit finding
    /// C1 (2026-05-19 QA): the controller previously had no Delete action,
    /// so the prompt's "delete 3 per module" couldn't be satisfied on the
    /// primary domain entity. Mirrors the cascade-guard pattern used by
    /// AssetCategoriesController / ServicesController — counts each
    /// dependent collection and refuses with a user-facing toast.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var process = await _context.Processes.FindAsync(id);
        if (process == null) return NotFound();

        // SEC-001: record-level scope (IDOR) on delete.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(process))
            return NotFound();

        var activityCount = await _context.Activities
            .CountAsync(a => a.ProcessId == id && !a.IsDeleted);
        var serviceLinkCount = await _context.ProcessServices
            .CountAsync(ps => ps.ProcessId == id && ps.IsActive);
        var riskCount = await _context.ProcessRisks
            .CountAsync(pr => pr.ProcessId == id && pr.IsActive);
        var responsibilityCount = await _context.ProcessResponsibilities
            .CountAsync(pr => pr.ProcessId == id && pr.IsActive);
        var objectiveCount = await _context.ProcessStrategicObjectives
            .CountAsync(pso => pso.ProcessId == id && pso.IsActive);
        var measurementCount = await _context.ProcessMeasurements
            .CountAsync(m => m.ProcessId == id);
        var raciCount = await _context.ProcessRacis
            .CountAsync(r => r.ProcessId == id);
        var assetCount = await _context.Assets
            .CountAsync(a => a.ProcessId == id && !a.IsDeleted);
        var incidentCount = await _context.Incidents
            .CountAsync(i => i.ProcessId == id && !i.IsDeleted);
        var problemCount = await _context.Problems
            .CountAsync(p => p.ProcessId == id && !p.IsDeleted);
        var improvementLinkCount = await _context.ImprovementProcesses
            .CountAsync(ip => ip.ProcessId == id);

        var total = activityCount + serviceLinkCount + riskCount + responsibilityCount
                  + objectiveCount + measurementCount + raciCount + assetCount
                  + incidentCount + problemCount + improvementLinkCount;
        if (total > 0)
        {
            var parts = new List<string>();
            if (activityCount > 0) parts.Add($"{activityCount} activity/ies");
            if (serviceLinkCount > 0) parts.Add($"{serviceLinkCount} service link(s)");
            if (riskCount > 0) parts.Add($"{riskCount} risk(s)");
            if (responsibilityCount > 0) parts.Add($"{responsibilityCount} responsibility link(s)");
            if (objectiveCount > 0) parts.Add($"{objectiveCount} strategic objective link(s)");
            if (measurementCount > 0) parts.Add($"{measurementCount} measurement(s)");
            if (raciCount > 0) parts.Add($"{raciCount} RACI row(s)");
            if (assetCount > 0) parts.Add($"{assetCount} asset(s)");
            if (incidentCount > 0) parts.Add($"{incidentCount} incident(s)");
            if (problemCount > 0) parts.Add($"{problemCount} problem(s)");
            if (improvementLinkCount > 0) parts.Add($"{improvementLinkCount} improvement link(s)");
            TempData["Error"] = $"Cannot delete this process — it has active dependencies: {string.Join(", ", parts)}. Reassign or remove them first.";
            return RedirectToAction(nameof(Details), new { id });
        }

        process.IsDeleted = true;
        process.DeletedAt = DateTime.UtcNow;
        process.UpdatedAt = DateTime.UtcNow;
        process.UpdatedById = User.Identity?.Name;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Process deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Returns the next DisplayOrder value to append a process to the end
    /// of its ProcessGroup. Increments by 10 so manual reorderings still
    /// have gaps to slot into without renumbering everything.
    /// </summary>
    private async Task<int> GetNextProcessDisplayOrderAsync(string? processGroupId)
    {
        if (string.IsNullOrWhiteSpace(processGroupId)) return 10;
        var maxOrder = await _context.Processes
            .Where(p => p.ProcessGroupId == processGroupId)
            .Select(p => (int?)p.DisplayOrder)
            .MaxAsync() ?? 0;
        return maxOrder + 10;
    }

    /// <summary>
    /// Generates the next process code under a given ProcessGroup in the
    /// strict hierarchical "X.Y.Z" scheme. Delegates to HierarchicalCodeService.
    /// </summary>
    private Task<string> GenerateNextProcessCodeAsync(string processGroupId)
        => _codeSvc.NextProcessCodeAsync(processGroupId);

    /// <summary>
    /// Lightweight AJAX endpoint that previews the next process code for a
    /// given parent ProcessGroup, so the Create view's Summary card can show
    /// "1.2.5" the moment the user picks a parent — no submit needed.
    /// Empty/unknown processGroupId returns code:null and the UI shows "—".
    /// Mirrors ProcessGroupsController.NextCode (L2).
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    public async Task<IActionResult> NextCode(string? processGroupId)
    {
        if (string.IsNullOrWhiteSpace(processGroupId))
            return Json(new { code = (string?)null });
        try
        {
            var code = await _codeSvc.NextProcessCodeAsync(processGroupId);
            return Json(new { code });
        }
        catch (InvalidOperationException)
        {
            return Json(new { code = (string?)null });
        }
    }

    private async Task PopulateDropdowns()
    {
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");

        var processGroups = await _context.ProcessGroups.Where(pg => !pg.IsDeleted).Include(pg => pg.Category).OrderBy(pg => pg.SortKey ?? pg.Code).ToListAsync();
        ViewBag.ProcessGroups = new SelectList(
            processGroups.Select(pg => new { pg.Id, DisplayName = isArabic ? pg.NameAr : pg.NameEn }),
            "Id", "DisplayName");

        var orgUnits = await _context.OrganizationUnits.Where(u => !u.IsDeleted && u.IsActive).OrderBy(u => u.Level).ThenBy(u => u.NameEn).ToListAsync();
        ViewBag.OrganizationUnits = new SelectList(
            orgUnits.Select(u => new { u.Id, DisplayName = isArabic ? u.NameAr : u.NameEn }),
            "Id", "DisplayName");

        var scope = await _scopingService.GetScopeAsync(User);
        var services = await _context.Services.Where(s => !s.IsDeleted).ApplyOwningUnitScope(scope).OrderBy(s => s.NameEn).ToListAsync();
        ViewBag.Services = new SelectList(
            services.Select(s => new { s.Id, DisplayName = isArabic ? s.NameAr : (!string.IsNullOrWhiteSpace(s.NameEn) && s.NameEn != s.NameAr ? s.NameEn : s.NameAr) }),
            "Id", "DisplayName");

        var objectives = await _context.StrategicObjectives.Where(so => !so.IsDeleted).OrderBy(so => so.NameEn).ToListAsync();
        ViewBag.StrategicObjectives = new SelectList(
            objectives.Select(so => new { so.Id, DisplayName = isArabic ? so.NameAr : so.NameEn }),
            "Id", "DisplayName");

        var systems = await _context.SystemDefinitions.Where(s => !s.IsDeleted).OrderBy(s => s.NameEn).ToListAsync();
        ViewBag.Systems = new SelectList(
            systems.Select(s => new { s.Id, DisplayName = isArabic ? s.NameAr : (!string.IsNullOrWhiteSpace(s.NameEn) && s.NameEn != s.NameAr ? s.NameEn : s.NameAr) }),
            "Id", "DisplayName");

        var assets = await _context.Assets.Where(a => !a.IsDeleted).ApplyAssignedUnitScope(scope).OrderBy(a => a.AssetTag).ToListAsync();
        ViewBag.Assets = new SelectList(
            assets.Select(a => new { a.Id, DisplayName = $"{a.AssetTag} - {(isArabic ? a.NameAr : a.NameEn)}" }),
            "Id", "DisplayName");

        var risks = await _context.EnterpriseRisks.Where(r => !r.IsDeleted).ApplyOrganizationScope(scope).OrderBy(r => r.RiskNumber).ToListAsync();
        ViewBag.Risks = new SelectList(
            risks.Select(r => new { r.Id, DisplayName = $"{r.RiskNumber} - {(isArabic ? r.NameAr : r.NameEn)}" }),
            "Id", "DisplayName");

        // Document Management lookups (Document Category / Document Type)
        var docCategories = await _context.DocumentCategories
            .Where(d => d.IsActive && !d.IsDeleted)
            .OrderBy(d => d.DisplayOrder)
            .ToListAsync();
        ViewBag.DocumentCategories = new SelectList(
            docCategories.Select(d => new { d.Id, DisplayName = isArabic ? d.NameAr : d.NameEn }),
            "Id", "DisplayName");

        var docTypes = await _context.DocumentTypes
            .Where(d => d.IsActive && !d.IsDeleted)
            .OrderBy(d => d.DisplayOrder)
            .ToListAsync();
        ViewBag.DocumentTypes = new SelectList(
            docTypes.Select(d => new { d.Id, DisplayName = isArabic ? d.NameAr : d.NameEn }),
            "Id", "DisplayName");

        // DocumentLanguage enum (Arabic / English / Bilingual)
        ViewBag.DocumentLanguages = Enum.GetValues<Models.Enums.DocumentLanguage>()
            .Select(dl => new
            {
                Value = dl.ToString(),
                DisplayName = dl switch
                {
                    Models.Enums.DocumentLanguage.Arabic    => isArabic ? "العربية" : "Arabic",
                    Models.Enums.DocumentLanguage.English   => isArabic ? "الإنجليزية" : "English",
                    Models.Enums.DocumentLanguage.Bilingual => isArabic ? "ثنائي اللغة" : "Bilingual",
                    _ => dl.ToString()
                }
            })
            .ToList();

        // AutomationStatus enum (Traditional / Semi-Automated / Automated)
        ViewBag.AutomationStatuses = Enum.GetValues<Models.Enums.AutomationStatus>()
            .Select(a => new
            {
                Value = a.ToString(),
                DisplayName = a switch
                {
                    Models.Enums.AutomationStatus.Traditional   => isArabic ? "غير مؤتمتة" : "Not Automated",
                    Models.Enums.AutomationStatus.SemiAutomated => isArabic ? "شبه مؤتمتة" : "Semi-Automated",
                    Models.Enums.AutomationStatus.Automated     => isArabic ? "مؤتمتة"      : "Automated",
                    _ => a.ToString()
                }
            })
            .ToList();

        // Localized ProcessType enum
        ViewBag.ProcessTypes = Enum.GetValues<Models.Enums.ProcessType>()
            .Select(pt => new { Value = pt.ToString(), DisplayName = _localizer[$"ProcessType_{pt}"].Value })
            .ToList();

        // Localized ProcessStatus enum
        ViewBag.ProcessStatuses = Enum.GetValues<Models.Enums.ProcessStatus>()
            .Select(ps => new { Value = ps.ToString(), DisplayName = _localizer[$"ProcessStatus_{ps}"].Value })
            .ToList();
    }

    /// <summary>
    /// Lightweight IOwnedByUnit carrier for IDOR scope checks on helper actions
    /// that only have a processId/owning-unit projection in hand (no full
    /// Process entity). Mirrors AssetsController.AssignedScopeProbe.
    /// </summary>
    private sealed record ProcessScopeProbe(int? OwningUnitId)
        : ESEMS.Web.Models.Common.IOwnedByUnit;
}

/// <summary>
/// Request model for updating BPMN diagram
/// </summary>
public class UpdateBpmnRequest
{
    public string BpmnXml { get; set; } = string.Empty;
    public string? ChangeDescription { get; set; }
}
