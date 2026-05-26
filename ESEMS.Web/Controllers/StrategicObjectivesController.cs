using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Integrations.Contracts;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Admin CRUD for Strategic Objectives (Level 1 / Tactical / Operational hierarchy).
///
/// When the external Process Performance integration is on, the local store
/// becomes read-only — Add/Edit/Retire are disabled and a banner directs the
/// user to the external system. This mirrors the EnterpriseRisksController
/// pattern for the risk integration.
///
/// SEC-013: strategic objectives are governance data that every process /
/// service / improvement aligns to. Mutating them now requires the
/// <see cref="AppPolicies.CanAdmin"/> policy rather than Improvement.Edit. The
/// class-level gate is relaxed to Improvement.View so existing viewers keep
/// read access to Index/Details; each write action carries its own CanAdmin
/// attribute.
///
/// Routes:
///   GET  /StrategicObjectives             — list active + retired (Improvement.View)
///   GET  /StrategicObjectives/Details/{id}— drill-down (Improvement.View)
///   GET  /StrategicObjectives/Create      — form (CanAdmin)
///   POST /StrategicObjectives/Create      — persist (CanAdmin)
///   GET  /StrategicObjectives/Edit/{id}   — form (CanAdmin)
///   POST /StrategicObjectives/Edit/{id}   — persist (CanAdmin)
///   POST /StrategicObjectives/Retire/{id} — soft-retire (IsActive=false) (CanAdmin)
///   POST /StrategicObjectives/Restore/{id}— un-retire (CanAdmin)
/// </summary>
[Authorize(Policy = AppPolicies.Module.Improvement.View)]
public class StrategicObjectivesController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<StrategicObjectivesController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IProcessPerformanceProvider _perf;

    public StrategicObjectivesController(
        ApplicationDbContext db,
        ILogger<StrategicObjectivesController> logger,
        IStringLocalizer<SharedResource> localizer,
        IProcessPerformanceProvider perf)
    {
        _db = db;
        _logger = logger;
        _localizer = localizer;
        _perf = perf;
    }

    private bool ExternalMode => _perf.IsEnabled;

    private void PopulateExternalModeViewBag()
    {
        ViewBag.ExternalMode         = ExternalMode;
        ViewBag.ExternalProviderName = _perf.ProviderName;
        ViewBag.ExternalIndexUrl     = _perf.GetIndexUrl();
        ViewBag.ExternalCreateUrl    = _perf.GetCreateUrl();
    }

    public async Task<IActionResult> Index()
    {
        var rows = await _db.StrategicObjectives
            .AsNoTracking()
            .Where(o => !o.IsDeleted)
            .Include(o => o.Parent)
            .OrderBy(o => o.Level)
            .ThenBy(o => o.DisplayOrder)
            .ThenBy(o => o.Code)
            .ToListAsync();
        PopulateExternalModeViewBag();
        return View(rows);
    }

    /// <summary>
    /// Read-only drill-down for an objective: shows hierarchy, owning unit,
    /// linked processes / services, and the current vs target value so the
    /// progress percentage on the Index page is fully explained. Added in
    /// response to the QA finding that the Index couldn't tell where the
    /// S0x progress percentages came from.
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var obj = await _db.StrategicObjectives
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.Id == id)
            .Include(o => o.Parent)
            .Include(o => o.Children.Where(c => !c.IsDeleted))
            .Include(o => o.OwningUnit)
            .Include(o => o.ProcessStrategicObjectives)
                .ThenInclude(pso => pso.Process)
            .Include(o => o.ServiceStrategicObjectives)
                .ThenInclude(sso => sso.Service)
            .FirstOrDefaultAsync();
        if (obj is null) return NotFound();

        PopulateExternalModeViewBag();
        return View(obj);
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Create()
    {
        if (ExternalMode) return RedirectExternal();
        await PopulateDropdownsAsync();
        return View(new StrategicObjective { TargetYear = DateTime.UtcNow.Year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Create(StrategicObjective model)
    {
        if (ExternalMode) return RedirectExternal();
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(model);
        }
        if (!string.IsNullOrWhiteSpace(model.Code) &&
            await _db.StrategicObjectives.AnyAsync(o => o.Code == model.Code && !o.IsDeleted))
        {
            ModelState.AddModelError(nameof(model.Code), "An objective with this code already exists.");
            await PopulateDropdownsAsync();
            return View(model);
        }
        model.Id = Guid.NewGuid().ToString();
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        model.CreatedById = User.Identity?.Name;
        if (model.DisplayOrder <= 0)
        {
            var nextOrder = await _db.StrategicObjectives
                .Where(o => o.Level == model.Level && !o.IsDeleted)
                .MaxAsync(o => (int?)o.DisplayOrder) ?? 0;
            model.DisplayOrder = nextOrder + 1;
        }
        _db.StrategicObjectives.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Objective '{model.Code}' added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Edit(string id)
    {
        if (ExternalMode) return RedirectExternal();
        var obj = await _db.StrategicObjectives.FindAsync(id);
        if (obj is null) return NotFound();
        await PopulateDropdownsAsync(obj.Id);
        return View(obj);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Edit(string id, StrategicObjective model)
    {
        if (ExternalMode) return RedirectExternal();
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(id);
            return View(model);
        }

        var existing = await _db.StrategicObjectives.FindAsync(id);
        if (existing is null) return NotFound();
        if (existing.Code != model.Code &&
            !string.IsNullOrWhiteSpace(model.Code) &&
            await _db.StrategicObjectives.AnyAsync(o => o.Code == model.Code && o.Id != id && !o.IsDeleted))
        {
            ModelState.AddModelError(nameof(model.Code), "Another objective with this code already exists.");
            await PopulateDropdownsAsync(id);
            return View(model);
        }

        existing.Code = model.Code;
        existing.NameEn = model.NameEn;
        existing.NameAr = model.NameAr;
        existing.DescriptionEn = model.DescriptionEn;
        existing.DescriptionAr = model.DescriptionAr;
        existing.Level = model.Level;
        existing.ParentId = string.IsNullOrWhiteSpace(model.ParentId) ? null : model.ParentId;
        existing.TargetYear = model.TargetYear;
        existing.TargetValue = model.TargetValue;
        existing.CurrentValue = model.CurrentValue;
        existing.UnitOfMeasure = model.UnitOfMeasure;
        existing.OwningUnitId = model.OwningUnitId;
        existing.Tags = model.Tags;
        existing.IsActive = model.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedById = User.Identity?.Name;
        existing.Version++;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Objective '{model.Code}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Retire(string id)
    {
        if (ExternalMode) return RedirectExternal();
        var obj = await _db.StrategicObjectives.FindAsync(id);
        if (obj is null) return NotFound();
        obj.IsActive = false;
        obj.UpdatedAt = DateTime.UtcNow;
        obj.UpdatedById = User.Identity?.Name;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Objective '{obj.Code}' retired.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Restore(string id)
    {
        if (ExternalMode) return RedirectExternal();
        var obj = await _db.StrategicObjectives.FindAsync(id);
        if (obj is null) return NotFound();
        obj.IsActive = true;
        obj.UpdatedAt = DateTime.UtcNow;
        obj.UpdatedById = User.Identity?.Name;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Objective '{obj.Code}' restored.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(string? excludeId = null)
    {
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
        var parents = await _db.StrategicObjectives
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.IsActive && o.Id != excludeId)
            .OrderBy(o => o.Level).ThenBy(o => o.DisplayOrder)
            .Select(o => new { o.Id, Display = o.Code + " — " + (isArabic ? o.NameAr : o.NameEn) })
            .ToListAsync();
        ViewBag.Parents = new SelectList(parents, "Id", "Display");

        // OrderBy/Select must use mapped columns (NameEn/NameAr), not the computed
        // `.Name` property — EF can't translate that to SQL. Pick the culture-appropriate
        // column server-side for ordering, project to a simple shape for the SelectList.
        var unitsRaw = await _db.OrganizationUnits
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.IsActive)
            .OrderBy(u => isArabic ? u.NameAr : u.NameEn)
            .Select(u => new { u.Id, u.NameEn, u.NameAr })
            .ToListAsync();
        var units = unitsRaw
            .Select(u => new { u.Id, Display = isArabic ? (u.NameAr ?? u.NameEn) : (u.NameEn ?? u.NameAr) })
            .ToList();
        ViewBag.OrganizationUnits = new SelectList(units, "Id", "Display");
    }

    private IActionResult RedirectExternal()
    {
        TempData["Info"] = $"Strategic objectives are managed in {_perf.ProviderName}. Local CRUD is disabled while the integration is active.";
        return RedirectToAction(nameof(Index));
    }
}
