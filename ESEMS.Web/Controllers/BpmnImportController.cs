using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Bpmn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Admin-only hub for the Visio-embedded-in-Excel → BPMN batch importer.
/// The actual conversion runs in <see cref="Api.ImportController.ImportExcelVisioBpmn"/>;
/// this controller just renders pages that post to that endpoint, surfaces
/// per-sheet results, and lets operators reconcile BPMN lanes whose names
/// didn't auto-match an OrganizationUnit.
/// </summary>
[Authorize(Policy = AppPolicies.CanAdmin)]
public class BpmnImportController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IBpmnLaneReconciler _reconciler;
    private readonly ILogger<BpmnImportController> _logger;

    public BpmnImportController(
        ApplicationDbContext db,
        IBpmnLaneReconciler reconciler,
        ILogger<BpmnImportController> logger)
    {
        _db = db;
        _reconciler = reconciler;
        _logger = logger;
    }

    public IActionResult Index() => View();

    /// <summary>
    /// Lane review queue. Lists every BpmnLane row, grouped by current
    /// resolution status, so an admin can map unresolved lanes manually,
    /// confirm auto-created units, or mark lanes as deliberately not
    /// representing an org unit.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Lanes()
    {
        var lanes = await _db.BpmnLanes
            .Include(l => l.Process)
            .Include(l => l.OrganizationUnit)
            .OrderBy(l => l.MatchMethod == BpmnLane.MatchMethods.Pending ? 0
                       : l.MatchMethod == BpmnLane.MatchMethods.AutoCreated ? 1
                       : l.MatchMethod == BpmnLane.MatchMethods.Normalized ? 2 : 3)
            .ThenBy(l => l.Name)
            .ToListAsync();

        var orgUnits = await _db.OrganizationUnits
            .Where(u => u.IsActive)
            .OrderBy(u => u.Level).ThenBy(u => u.NameEn)
            .Select(u => new { u.Id, u.NameEn, u.NameAr, u.Code, u.Level })
            .ToListAsync();

        ViewBag.OrgUnits = orgUnits;
        return View(lanes);
    }

    /// <summary>
    /// Manual mapping action — sets a lane's OrganizationUnitId and stamps
    /// MatchMethod=Manual so future re-imports don't overwrite the choice.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MapLane(string id, string? organizationUnitId, string? action)
    {
        var lane = await _db.BpmnLanes.FirstOrDefaultAsync(l => l.Id == id);
        if (lane == null) return NotFound();

        switch (action)
        {
            case "ignore":
                lane.OrganizationUnitId = null;
                lane.MatchMethod = BpmnLane.MatchMethods.Ignored;
                break;
            case "reset":
                lane.OrganizationUnitId = null;
                lane.MatchMethod = BpmnLane.MatchMethods.Pending;
                lane.MatchedAt = null;
                lane.MatchedById = null;
                break;
            default:
                if (string.IsNullOrWhiteSpace(organizationUnitId)
                    || !int.TryParse(organizationUnitId, out var organizationUnitIdInt))
                    return BadRequest("organizationUnitId is required for mapping");
                // Validate the target unit exists + is active before we
                // assign. Without this check the FK constraint would still
                // reject the insert, but the EF error leaks schema details
                // back to the operator. A clean BadRequest is friendlier.
                var unitExists = await _db.OrganizationUnits
                    .AnyAsync(u => u.Id == organizationUnitIdInt && u.IsActive);
                if (!unitExists)
                    return BadRequest("Org unit not found or inactive");
                lane.OrganizationUnitId = organizationUnitIdInt;
                lane.MatchMethod = BpmnLane.MatchMethods.Manual;
                lane.MatchedAt = DateTime.UtcNow;
                lane.MatchedById = User?.Identity?.Name;
                break;
        }

        lane.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // If a lane was just mapped (not ignored/reset), back-fill the
        // activities inside this lane so the operator sees the effect.
        if (action != "ignore" && action != "reset" && lane.OrganizationUnitId != null
            && !string.IsNullOrWhiteSpace(lane.FlowNodeRefsJson))
        {
            try
            {
                var refs = System.Text.Json.JsonSerializer.Deserialize<List<string>>(lane.FlowNodeRefsJson) ?? new();
                if (refs.Count > 0)
                {
                    var activities = await _db.Activities
                        .Where(a => a.ProcessId == lane.ProcessId &&
                                    (refs.Contains(a.Id) || refs.Contains(a.Code)))
                        .ToListAsync();
                    foreach (var a in activities)
                    {
                        if (a.OwningUnitId == null || a.OwningUnitId == lane.OrganizationUnitId)
                            a.OwningUnitId = lane.OrganizationUnitId;
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Activity back-fill failed for lane {LaneId}; mapping saved without back-fill", id);
            }
        }

        return RedirectToAction(nameof(Lanes));
    }

    /// <summary>
    /// Re-run reconcile for a single Process — useful after the operator
    /// renames/creates org units and wants to retry the auto matcher
    /// without re-importing the BPMN.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RerunReconcile(string processId, bool autoCreate = false)
    {
        var process = await _db.Processes.FirstOrDefaultAsync(p => p.Id == processId);
        if (process == null || string.IsNullOrWhiteSpace(process.BpmnDiagram)) return NotFound();

        var result = await _reconciler.ReconcileAsync(processId, process.BpmnDiagram, autoCreate, User?.Identity?.Name);
        TempData["LaneReconcileResult"] =
            $"Process {processId}: seen={result.LanesSeen} matched={result.Matched} autoCreated={result.AutoCreated} unmatched={result.Unmatched} activitiesBackfilled={result.ActivitiesBackfilled}";
        return RedirectToAction(nameof(Lanes));
    }
}
