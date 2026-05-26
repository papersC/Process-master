using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.ServiceManagement;
using ESEMS.Web.Extensions;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Common;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for Incident Management (ISO 20000-1:2018)
/// </summary>
[Authorize(Policy = AppPolicies.Module.Incident.View)]
public class IncidentsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<IncidentsController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IEntityNumberGenerator _numberGenerator;
    private readonly IScopingService _scopingService;

    public IncidentsController(
        ApplicationDbContext context,
        ILogger<IncidentsController> logger,
        IStringLocalizer<SharedResource> localizer,
        IEntityNumberGenerator numberGenerator,
        IScopingService scopingService)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _numberGenerator = numberGenerator;
        _scopingService = scopingService;
    }

    /// <summary>
    /// List all incidents
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var query = _context.Incidents
            .Where(i => !i.IsDeleted)
            .Include(i => i.Service)
            .Include(i => i.Process)
            .Include(i => i.Asset)
            .Include(i => i.AssignedToUnit)
            .Include(i => i.Problem)
            .AsQueryable();

        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyAssignedUnitScope(scope);

        var incidents = await query.OrderByDescending(i => i.ReportedAt).ToListAsync();

        // Re-evaluate SLA status on read so the list-page badges ("Breached",
        // "X h remaining") reflect the CURRENT clock — not the value stored
        // on the last save. Without this, an incident that crossed its
        // SlaDueDate hours after the last edit shows "remaining: -548h" on
        // the row but "Breached" KPI stays at the stale count.
        foreach (var i in incidents) i.CheckSlaStatus();

        return View(incidents);
    }

    /// <summary>
    /// View incident details
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var incident = await _context.Incidents
            .Include(i => i.Service)
            .Include(i => i.Process)
            .Include(i => i.Asset)
            .Include(i => i.AssignedToUnit)
            .Include(i => i.Problem)
            .Include(i => i.Comments)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        if (incident == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(incident))
            return NotFound();

        // Check SLA status
        incident.CheckSlaStatus();

        return View(incident);
    }

    /// <summary>
    /// Create incident form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Incident.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        var incident = new Incident
        {
            IncidentNumber = await _numberGenerator.GenerateNextNumberAsync("INC"),
            ReportedAt = DateTime.UtcNow
        };
        incident.CalculateSlaDueDate();
        return View(incident);
    }

    /// <summary>
    /// Create incident
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Incident.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Incident incident)
    {
        if (ModelState.IsValid)
        {
            incident.Id = Guid.NewGuid().ToString();
            incident.IncidentNumber = await _numberGenerator.GenerateNextNumberAsync("INC");
            incident.ReportedAt = DateTime.UtcNow;
            incident.CalculateSlaDueDate();
            incident.CreatedAt = DateTime.UtcNow;
            incident.UpdatedAt = DateTime.UtcNow;

            _context.Incidents.Add(incident);
            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_IncidentCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = incident.Id });
        }

        await PopulateDropdowns();
        return View(incident);
    }

    /// <summary>
    /// Edit incident form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Incident.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var incident = await _context.Incidents
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        if (incident == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(incident))
            return NotFound();

        await PopulateDropdowns();
        return View(incident);
    }

    /// <summary>
    /// Edit incident
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Incident.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Incident incident)
    {
        if (id != incident.Id)
            return NotFound();

        // IDOR mass-assignment defense: check scope on persisted unit FK.
        var existing = await _context.Incidents.AsNoTracking()
            .Where(i => i.Id == id && !i.IsDeleted)
            .Select(i => new { i.AssignedToUnitId })
            .FirstOrDefaultAsync();
        if (existing == null) return NotFound();
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(new IncidentScopeProbe(existing.AssignedToUnitId)))
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                // FUNC-001: load-then-patch instead of _context.Update(boundEntity).
                // The Edit form posts hidden CreatedAt/CreatedById/Version/
                // ResolvedAt/ClosedAt/IncidentNumber fields — a crafted POST could
                // forge any of them (incl. IsDeleted). Load the tracked row and copy
                // ONLY the user-editable fields the form actually exposes; lifecycle
                // timestamps (ResolvedAt/ClosedAt) are owned by Resolve/Close, and
                // IncidentNumber/ReportedAt stay as created.
                var tracked = await _context.Incidents
                    .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
                if (tracked == null)
                    return NotFound();

                tracked.NameEn = incident.NameEn;
                tracked.NameAr = incident.NameAr;
                tracked.DescriptionEn = incident.DescriptionEn;
                tracked.DescriptionAr = incident.DescriptionAr;
                tracked.Category = incident.Category;
                tracked.Subcategory = incident.Subcategory;
                tracked.Status = incident.Status;
                tracked.Priority = incident.Priority;
                tracked.Impact = incident.Impact;
                tracked.Urgency = incident.Urgency;
                tracked.ServiceId = incident.ServiceId;
                tracked.ProcessId = incident.ProcessId;
                tracked.AssetId = incident.AssetId;
                tracked.AssignedToUnitId = incident.AssignedToUnitId;
                tracked.Workaround = incident.Workaround;
                tracked.RootCause = incident.RootCause;
                tracked.ResolutionNotes = incident.ResolutionNotes;
                tracked.SlaTargetHours = incident.SlaTargetHours;

                tracked.UpdatedAt = DateTime.UtcNow;
                tracked.UpdatedById = User.Identity?.Name;
                tracked.Version++;
                tracked.CheckSlaStatus();
                await _context.SaveChangesAsync();

                TempData["Success"] = _localizer["Success_IncidentUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id = tracked.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await IncidentExists(incident.Id))
                    return NotFound();
                throw;
            }
        }

        await PopulateDropdowns();
        return View(incident);
    }

    /// <summary>
    /// Delete incident
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Incident.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var incident = await _context.Incidents.FindAsync(id);
        if (incident != null)
        {
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(incident))
                return NotFound();

            incident.IsDeleted = true;
            incident.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = _localizer["Success_IncidentDeleted"].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    private sealed record IncidentScopeProbe(int? AssignedToUnitId)
        : ESEMS.Web.Models.Common.IAssignedToUnit;

    /// <summary>
    /// Resolve incident
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Incident.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(string id, string resolutionNotes)
    {
        var incident = await _context.Incidents.FindAsync(id);
        if (incident == null)
            return NotFound();

        // FUNC-016: scope guard — this endpoint mutates by id with no scope check
        // before. Mirror Edit/Details so a scoped user can't resolve an incident
        // outside their org-unit subtree by URL tampering.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(incident))
            return NotFound();

        // FLOW-004: state guard. Resolve is only legal from an open state
        // (New / InProgress). Re-resolving an already-Resolved or Closed incident
        // would silently overwrite ResolvedAt and the resolution notes.
        if (incident.Status != Models.Enums.IncidentStatus.New &&
            incident.Status != Models.Enums.IncidentStatus.InProgress)
        {
            TempData["Error"] = $"Cannot resolve an incident in status '{incident.Status}'. Only New or In Progress incidents can be resolved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        incident.Status = Models.Enums.IncidentStatus.Resolved;
        incident.ResolvedAt = DateTime.UtcNow;
        incident.ResolutionNotes = resolutionNotes;
        incident.UpdatedAt = DateTime.UtcNow;
        incident.UpdatedById = User.Identity?.Name;
        incident.CheckSlaStatus();
        await _context.SaveChangesAsync();
        TempData["Success"] = _localizer["Success_IncidentResolved"].Value;

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Close incident
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Incident.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(string id)
    {
        var incident = await _context.Incidents.FindAsync(id);
        if (incident == null)
            return NotFound();

        // FUNC-016: scope guard — same rationale as Resolve.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(incident))
            return NotFound();

        // FLOW-004: state guard. Close is only legal from Resolved. Closing
        // straight from an open state would skip resolution and overwrite ClosedAt.
        if (incident.Status != Models.Enums.IncidentStatus.Resolved)
        {
            TempData["Error"] = $"Cannot close an incident in status '{incident.Status}'. Only Resolved incidents can be closed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        incident.Status = Models.Enums.IncidentStatus.Closed;
        incident.ClosedAt = DateTime.UtcNow;
        incident.UpdatedAt = DateTime.UtcNow;
        incident.UpdatedById = User.Identity?.Name;
        await _context.SaveChangesAsync();
        TempData["Success"] = _localizer["Success_IncidentClosed"].Value;

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Services = new SelectList(await _context.Services.Where(s => !s.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.Processes = new SelectList(await _context.Processes.Where(p => !p.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.Assets = new SelectList(await _context.Assets.Where(a => !a.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.OrganizationUnits = new SelectList(await _context.OrganizationUnits.Where(o => !o.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.Problems = new SelectList(await _context.Problems.Where(p => !p.IsDeleted).ToListAsync(), "Id", "ProblemNumber");
    }

    private async Task<bool> IncidentExists(string id)
    {
        return await _context.Incidents.AnyAsync(i => i.Id == id && !i.IsDeleted);
    }
}
