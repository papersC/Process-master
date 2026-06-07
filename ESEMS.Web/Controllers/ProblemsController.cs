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
/// Controller for Problem Management (ISO 20000-1:2018)
/// Manages root cause analysis and permanent solutions for recurring incidents
/// </summary>
[Authorize(Policy = AppPolicies.Module.Problem.View)]
public class ProblemsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProblemsController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IEntityNumberGenerator _numberGenerator;
    private readonly IScopingService _scopingService;

    public ProblemsController(
        ApplicationDbContext context,
        ILogger<ProblemsController> logger,
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

    public async Task<IActionResult> Index()
    {
        var query = _context.Problems
            .Where(p => !p.IsDeleted)
            .Include(p => p.Service)
            .Include(p => p.Process)
            .Include(p => p.Asset)
            .Include(p => p.AssignedToUnit)
            .Include(p => p.RelatedIncidents)
            .AsQueryable();

        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyAssignedUnitScope(scope);

        var problems = await query.OrderByDescending(p => p.IdentifiedAt).ToListAsync();

        return View(problems);
    }

    public async Task<IActionResult> Details(string id)
    {
        var problem = await _context.Problems
            .Include(p => p.Service)
            .Include(p => p.Process)
            .Include(p => p.Asset)
            .Include(p => p.AssignedToUnit)
            .Include(p => p.RelatedIncidents)
            .Include(p => p.Comments)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (problem == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(problem))
            return NotFound();

        problem.UpdateIncidentCount();

        return View(problem);
    }

    [Authorize(Policy = AppPolicies.Module.Problem.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        var problem = new Problem
        {
            ProblemNumber = await _numberGenerator.GenerateNextNumberAsync("PRB"),
            IdentifiedAt = DateTime.UtcNow
        };
        return View(problem);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Problem.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Problem problem)
    {
        if (ModelState.IsValid)
        {
            problem.Id = Guid.NewGuid().ToString();
            problem.ProblemNumber = await _numberGenerator.GenerateNextNumberAsync("PRB");
            problem.IdentifiedAt = DateTime.UtcNow;
            problem.CreatedAt = DateTime.UtcNow;
            problem.UpdatedAt = DateTime.UtcNow;

            _context.Problems.Add(problem);
            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_ProblemCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = problem.Id });
        }

        await PopulateDropdowns();
        return View(problem);
    }

    [Authorize(Policy = AppPolicies.Module.Problem.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var problem = await _context.Problems
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (problem == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(problem))
            return NotFound();

        await PopulateDropdowns();
        return View(problem);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Problem.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Problem problem)
    {
        if (id != problem.Id)
            return NotFound();

        // IDOR mass-assignment defense: check scope on persisted unit FK.
        var existing = await _context.Problems.AsNoTracking()
            .Where(p => p.Id == id && !p.IsDeleted)
            .Select(p => new { p.AssignedToUnitId })
            .FirstOrDefaultAsync();
        if (existing == null) return NotFound();
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(new ProblemScopeProbe(existing.AssignedToUnitId)))
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                // FUNC-001: load-then-patch instead of _context.Update(boundEntity).
                // The Edit form posts hidden CreatedAt/CreatedById/Version fields —
                // a crafted POST could forge any of them (incl. IsDeleted/DeletedAt).
                // Load the tracked row and copy ONLY the user-editable fields the
                // form exposes; ProblemNumber/IdentifiedAt stay as created, and
                // lifecycle fields (ResolvedAt/ClosedAt) are left untouched.
                var tracked = await _context.Problems
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
                if (tracked == null)
                    return NotFound();

                tracked.NameEn = problem.NameEn;
                tracked.NameAr = problem.NameAr;
                tracked.DescriptionEn = problem.DescriptionEn;
                tracked.DescriptionAr = problem.DescriptionAr;
                tracked.Status = problem.Status;
                tracked.Category = problem.Category;
                tracked.Priority = problem.Priority;
                tracked.Impact = problem.Impact;
                tracked.IsKnownError = problem.IsKnownError;
                tracked.EstimatedCostImpact = problem.EstimatedCostImpact;
                tracked.ServiceId = problem.ServiceId;
                tracked.ProcessId = problem.ProcessId;
                tracked.AssetId = problem.AssetId;
                tracked.AssignedToUnitId = problem.AssignedToUnitId;
                tracked.RootCauseAnalysis = problem.RootCauseAnalysis;
                tracked.Workaround = problem.Workaround;
                tracked.PermanentSolution = problem.PermanentSolution;

                tracked.UpdatedAt = DateTime.UtcNow;
                tracked.UpdatedById = User.Identity?.Name;
                tracked.Version++;
                tracked.UpdateIncidentCount();
                await _context.SaveChangesAsync();

                TempData["Success"] = _localizer["Success_ProblemUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id = tracked.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ProblemExists(problem.Id))
                    return NotFound();
                throw;
            }
        }

        await PopulateDropdowns();
        return View(problem);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Problem.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var problem = await _context.Problems.FindAsync(id);
        if (problem != null)
        {
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(problem))
                return NotFound();

            problem.IsDeleted = true;
            problem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = _localizer["Success_ProblemDeleted"].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    private sealed record ProblemScopeProbe(int? AssignedToUnitId)
        : ESEMS.Web.Models.Common.IAssignedToUnit;

    /// <summary>
    /// Escalate an Incident to a Problem (ISO 20000-1 best practice)
    /// Pre-fills a new Problem with the incident's data and links them
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Problem.Create)]
    public async Task<IActionResult> CreateFromIncident(string incidentId)
    {
        var incident = await _context.Incidents
            .Include(i => i.Service)
            .Include(i => i.Process)
            .Include(i => i.Asset)
            .FirstOrDefaultAsync(i => i.Id == incidentId && !i.IsDeleted);

        if (incident == null)
            return NotFound();

        // SEC (IDOR): don't escalate — or leak the details of — an incident
        // outside the caller's scope.
        if (!(await _scopingService.GetScopeAsync(User)).CanAccess(incident)) return NotFound();

        await PopulateDropdowns();

        var problem = new Problem
        {
            ProblemNumber = await _numberGenerator.GenerateNextNumberAsync("PRB"),
            IdentifiedAt = DateTime.UtcNow,
            NameEn = $"[Escalated] {incident.NameEn}",
            NameAr = !string.IsNullOrEmpty(incident.NameAr) ? $"[مُصعَّد] {incident.NameAr}" : string.Empty,
            DescriptionEn = $"Escalated from Incident {incident.IncidentNumber}.\n\n{incident.DescriptionEn}",
            DescriptionAr = !string.IsNullOrEmpty(incident.DescriptionAr) ? $"تم التصعيد من الحادث {incident.IncidentNumber}.\n\n{incident.DescriptionAr}" : string.Empty,
            Category = incident.Category ?? "Other",
            Priority = incident.Priority,
            Impact = incident.Impact,
            ServiceId = incident.ServiceId,
            ProcessId = incident.ProcessId,
            AssetId = incident.AssetId,
            AssignedToUnitId = incident.AssignedToUnitId
        };

        ViewBag.EscalatedFromIncidentId = incidentId;
        ViewBag.EscalatedFromIncidentNumber = incident.IncidentNumber;

        return View("Create", problem);
    }

    /// <summary>
    /// Create problem from escalated incident (POST)
    /// Links the problem back to the incident
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Problem.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromIncident(Problem problem, string? escalatedFromIncidentId)
    {
        if (ModelState.IsValid)
        {
            problem.Id = Guid.NewGuid().ToString();
            problem.ProblemNumber = await _numberGenerator.GenerateNextNumberAsync("PRB");
            problem.IdentifiedAt = DateTime.UtcNow;
            problem.CreatedAt = DateTime.UtcNow;
            problem.UpdatedAt = DateTime.UtcNow;

            _context.Problems.Add(problem);

            // Link the incident to this new problem
            if (!string.IsNullOrEmpty(escalatedFromIncidentId))
            {
                var incident = await _context.Incidents.FindAsync(escalatedFromIncidentId);
                if (incident != null)
                {
                    incident.ProblemId = problem.Id;
                    incident.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_ProblemCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = problem.Id });
        }

        await PopulateDropdowns();
        return View("Create", problem);
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Services = new SelectList(await _context.Services.Where(s => !s.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.Processes = new SelectList(await _context.Processes.Where(p => !p.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.Assets = new SelectList(await _context.Assets.Where(a => !a.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.OrganizationUnits = new SelectList(await _context.OrganizationUnits.Where(o => !o.IsDeleted).ToListAsync(), "Id", "Name");
    }

    private async Task<bool> ProblemExists(string id)
    {
        return await _context.Problems.AnyAsync(p => p.Id == id && !p.IsDeleted);
    }
}
