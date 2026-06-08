using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Extensions;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Bpmn;
using ESEMS.Web.Services.Common;
using System.Globalization;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Pure-DB BPMN read actions extracted from the fat <see cref="AIController"/>.
/// These return existing BPMN XML + metadata for the Wizard/Diagrams editors
/// and don't touch any AI service, so keeping them separate shrinks the AI
/// surface and makes the BPMN-viewer flow clearer.
///
/// URLs are preserved via <c>[Route("AI")]</c> — JS callers continue to hit
/// <c>/AI/LoadProcessBPMN</c>, <c>/AI/GetProcessesWithBPMN</c>, etc.
/// </summary>
[Authorize(Policy = AppPolicies.Module.Process.View)]
[Route("AI")]
public class AIBpmnReadController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AIBpmnReadController> _logger;
    private readonly IBpmnProcessingService _bpmnService;
    private readonly IScopingService _scopingService;

    public AIBpmnReadController(
        ApplicationDbContext context,
        ILogger<AIBpmnReadController> logger,
        IBpmnProcessingService bpmnService,
        IScopingService scopingService)
    {
        _context = context;
        _logger = logger;
        _bpmnService = bpmnService;
        _scopingService = scopingService;
    }

    private static bool IsArabic => CultureInfo.CurrentUICulture.Name.StartsWith("ar");

    /// <summary>
    /// SEC-003: per-record IDOR guard. A scoped user may only read the BPMN of
    /// a process within their visible org-unit subtree. Unscoped (admin) users
    /// always pass. The <see cref="Models.APQC.Process"/> entity implements
    /// <c>IOwnedByUnit</c>, so the existing <c>ScopeContext.CanAccess</c>
    /// overload applies.
    /// </summary>
    private async Task<bool> CanAccessProcessAsync(Models.APQC.Process process)
    {
        var scope = await _scopingService.GetScopeAsync(User);
        return scope.CanAccess(process);
    }

    /// <summary>
    /// SEC-003: per-record IDOR guard for a ProcessTask, resolved through its
    /// parent process (ProcessTask → Activity → Process). A task whose parent
    /// process is out of scope is treated as inaccessible. Tasks with no
    /// resolvable parent process fall back to the task's own OwningUnitId.
    /// </summary>
    private async Task<bool> CanAccessProcessTaskAsync(string processTaskId)
    {
        var scope = await _scopingService.GetScopeAsync(User);
        if (scope.IsUnscoped) return true;

        var owningUnitId = await _context.ProcessTasks
            .Where(pt => pt.Id == processTaskId && !pt.IsDeleted)
            .Select(pt => pt.Activity != null && pt.Activity.Process != null
                ? pt.Activity.Process.OwningUnitId
                : pt.OwningUnitId)
            .FirstOrDefaultAsync();

        // Null owning unit → orphan, visible to everyone (matches CanAccess contract).
        if (owningUnitId == null) return true;
        return scope.VisibleUnitIds != null && scope.VisibleUnitIds.Contains(owningUnitId.Value);
    }

    [HttpGet("GetProcessBpmn")]
    public async Task<IActionResult> GetProcessBpmn(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Json(new { success = false, error = "Process ID is required." });

        var process = await _context.Processes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        // SEC-003: record-level scope (IDOR). Block direct-URL access to a
        // process outside the caller's org scope. Treat as "no diagram" so
        // we don't leak the existence of out-of-scope processes.
        if (process == null || !await CanAccessProcessAsync(process))
            return Json(new { success = false, error = "No BPMN diagram." });

        if (string.IsNullOrWhiteSpace(process.BpmnDiagram))
            return Json(new { success = false, error = "No BPMN diagram." });

        return Json(new { success = true, xml = process.BpmnDiagram });
    }

    [HttpGet("GetProcesses")]
    public async Task<IActionResult> GetProcesses()
    {
        try
        {
            // SEC-003: list scoped to the caller's visible org units.
            var scope = await _scopingService.GetScopeAsync(User);
            var processes = await _context.Processes
                .Where(p => !p.IsDeleted)
                .ApplyOwningUnitScope(scope)
                .OrderBy(p => p.Code)
                .Select(p => new
                {
                    p.Id,
                    p.Code,
                    Name = IsArabic ? p.NameAr : p.NameEn
                })
                .ToListAsync();

            return Json(new { success = true, processes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processes");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    [HttpGet("GetProcessesWithBPMN")]
    public async Task<IActionResult> GetProcessesWithBPMN()
    {
        try
        {
            // SEC-003: list scoped to the caller's visible org units.
            var scope = await _scopingService.GetScopeAsync(User);
            var processes = await _context.Processes
                .Where(p => !p.IsDeleted && !string.IsNullOrWhiteSpace(p.BpmnDiagram))
                .ApplyOwningUnitScope(scope)
                .OrderBy(p => p.Code)
                .Select(p => new
                {
                    p.Id,
                    p.Code,
                    Name = IsArabic ? p.NameAr : p.NameEn,
                    HasBpmn = true
                })
                .ToListAsync();

            return Json(new { success = true, processes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processes with BPMN");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// "Enhance Drawing" — loads a Process's BPMN and pipes it through
    /// <see cref="IBpmnProcessingService.EnhanceBpmnLayout"/>, which
    /// re-routes every edge with orthogonal waypoints anchored to shape
    /// boundaries, centres edge labels on the new midpoints, resizes
    /// tasks whose labels visibly overflow, and runs the standard
    /// diagonal/RTL/colour repair pipeline along the way. Returns the
    /// enhanced XML alongside the process metadata so the modeler can
    /// import it directly.
    /// </summary>
    [HttpGet("EnhanceProcessBPMN")]
    public async Task<IActionResult> EnhanceProcessBPMN(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return Json(new { success = false, error = "Process ID is required." });

            var entity = await _context.Processes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            // Same SEC-003 IDOR guard as LoadProcessBPMN — never leak the
            // existence of an out-of-scope process via this endpoint.
            if (entity == null || !await CanAccessProcessAsync(entity))
                return Json(new { success = false, error = "Process not found." });

            if (string.IsNullOrWhiteSpace(entity.BpmnDiagram))
                return Json(new { success = false, error = "This process has no BPMN diagram." });

            var enhanced = _bpmnService.EnhanceBpmnLayout(entity.BpmnDiagram);

            return Json(new
            {
                success = true,
                processId = entity.Id,
                processCode = entity.Code,
                processName = IsArabic ? entity.NameAr : entity.NameEn,
                bpmnXml = enhanced
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enhancing BPMN from process {Id}", id);
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// "Enhance Drawing" for an ad-hoc XML payload — used when the user
    /// wants to clean up whatever is currently in the modeler (rather
    /// than a stored Process). Returns the enhanced XML.
    /// </summary>
    [HttpPost("EnhanceBpmnXml")]
    [ValidateAntiForgeryToken]
    public IActionResult EnhanceBpmnXml([FromBody] EnhanceXmlRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.BpmnXml))
            return Json(new { success = false, error = "BPMN XML is required." });
        try
        {
            var enhanced = _bpmnService.EnhanceBpmnLayout(req.BpmnXml);
            return Json(new { success = true, bpmnXml = enhanced });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnhanceBpmnXml failed");
            return Json(new { success = false, error = ex.Message });
        }
    }

    public class EnhanceXmlRequest
    {
        public string? BpmnXml { get; set; }
    }

    [HttpGet("LoadProcessBPMN")]
    public async Task<IActionResult> LoadProcessBPMN(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return Json(new { success = false, error = "Process ID is required." });

            var entity = await _context.Processes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            // SEC-003: 404-shaped response for out-of-scope processes so we
            // don't leak existence to a scoped user tampering with the id.
            if (entity == null || !await CanAccessProcessAsync(entity))
                return Json(new { success = false, error = "Process not found." });

            var process = new
            {
                entity.Id,
                entity.Code,
                Name = IsArabic ? entity.NameAr : entity.NameEn,
                entity.BpmnDiagram
            };

            if (string.IsNullOrWhiteSpace(process.BpmnDiagram))
                return Json(new { success = false, error = "This process has no BPMN diagram." });

            // Return the stored diagram VERBATIM — do not run CleanBpmnXml on a
            // diagram that is already saved. CleanBpmnXml is the AI-output repair
            // pipeline (re-routes waypoints, re-applies "standard" colours, adds
            // gateway labels); running it here silently rewrote the user's saved
            // edits, so "Load from Process" in the AI editor didn't match what the
            // Process page saved and showed. The Process Details viewer renders
            // BpmnDiagram raw, so returning it raw keeps both views identical.
            // (The explicit "Enhance Drawing" action still cleans, on demand.)
            return Json(new
            {
                success = true,
                processId = process.Id,
                processCode = process.Code,
                processName = process.Name,
                bpmnXml = process.BpmnDiagram
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading BPMN from process {Id}", id);
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    [HttpGet("GetProcessTasksWithBPMN")]
    public async Task<IActionResult> GetProcessTasksWithBPMN()
    {
        try
        {
            // SEC-003: scope tasks by their parent process's owning unit. A task
            // whose parent process (or, lacking one, the task itself) is owned by
            // a unit outside the caller's subtree is filtered out. Orphan tasks
            // (null owning unit) stay visible, matching the CanAccess contract.
            var scope = await _scopingService.GetScopeAsync(User);
            var query = _context.ProcessTasks
                .Where(pt => !pt.IsDeleted && !string.IsNullOrWhiteSpace(pt.BpmnDiagram))
                .Include(pt => pt.Activity)
                    .ThenInclude(a => a!.Process)
                .AsQueryable();

            if (!scope.IsUnscoped && scope.VisibleUnitIds != null)
            {
                query = query.Where(pt =>
                    (pt.Activity != null && pt.Activity.Process != null)
                        ? (pt.Activity.Process.OwningUnitId == null
                            || scope.VisibleUnitIds.Contains(pt.Activity.Process.OwningUnitId.Value))
                        : (pt.OwningUnitId == null || scope.VisibleUnitIds.Contains(pt.OwningUnitId.Value)));
            }

            var tasks = await query
                .OrderBy(pt => pt.Code)
                .Select(pt => new
                {
                    id = pt.Id,
                    code = pt.Code,
                    name = IsArabic ? pt.NameAr : pt.NameEn,
                    processName = pt.Activity != null && pt.Activity.Process != null
                        ? (IsArabic ? pt.Activity.Process.NameAr : pt.Activity.Process.NameEn)
                        : null
                })
                .ToListAsync();

            return Json(new { success = true, tasks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ProcessTasks with BPMN");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    [HttpGet("LoadProcessTaskBPMN")]
    public async Task<IActionResult> LoadProcessTaskBPMN(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return Json(new { success = false, error = "ProcessTask ID is required." });

            var task = await _context.ProcessTasks
                .Where(pt => pt.Id == id && !pt.IsDeleted)
                .Select(pt => new
                {
                    pt.Id,
                    pt.Code,
                    Name = IsArabic ? pt.NameAr : pt.NameEn,
                    pt.BpmnDiagram
                })
                .FirstOrDefaultAsync();

            // SEC-003: per-record IDOR guard via the task's parent process.
            if (task == null || !await CanAccessProcessTaskAsync(task.Id))
                return Json(new { success = false, error = "ProcessTask not found." });

            if (string.IsNullOrWhiteSpace(task.BpmnDiagram))
                return Json(new { success = false, error = "This procedure has no BPMN diagram." });

            // Return the stored procedure diagram verbatim — see LoadProcessBPMN:
            // re-cleaning an already-saved diagram overwrites the user's edits and
            // desyncs the AI viewer from what the Process page shows.
            return Json(new
            {
                success = true,
                processTaskId = task.Id,
                processTaskCode = task.Code,
                processTaskName = task.Name,
                bpmnXml = task.BpmnDiagram
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading BPMN from ProcessTask {Id}", id);
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }
}
