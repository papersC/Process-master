using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Integrations.Contracts;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Audit #15 — admin CRUD for the central KPI catalog.
///
/// SEC-013: the KPI catalog is governance data. Mutating it (Create/Edit/
/// Retire/Restore) now requires the <see cref="AppPolicies.CanAdmin"/> policy,
/// not merely Improvement.Edit — a process owner who can edit an improvement
/// should not be able to rewrite the org-wide KPI definitions other initiatives
/// depend on. The class-level gate is relaxed to Improvement.View so existing
/// viewers can still browse the catalog (Index) and the typeahead Search; each
/// write action carries its own CanAdmin attribute.
///
/// Routes:
///   GET  /KpiLibrary             -> list active + retired KPIs (Improvement.View)
///   GET  /KpiLibrary/Create      -> form (CanAdmin)
///   POST /KpiLibrary/Create      -> persist (CanAdmin)
///   GET  /KpiLibrary/Edit/{id}   -> form (CanAdmin)
///   POST /KpiLibrary/Edit/{id}   -> persist (CanAdmin)
///   POST /KpiLibrary/Retire/{id} -> soft-retire (IsActive = false) (CanAdmin)
///   POST /KpiLibrary/Restore/{id}-> un-retire (CanAdmin)
/// </summary>
[Authorize(Policy = AppPolicies.Module.Improvement.View)]
public class KpiLibraryController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<KpiLibraryController> _logger;
    private readonly IProcessPerformanceProvider _perf;

    public KpiLibraryController(
        ApplicationDbContext db,
        ILogger<KpiLibraryController> logger,
        IProcessPerformanceProvider perf)
    {
        _db = db;
        _logger = logger;
        _perf = perf;
    }

    /// <summary>True when an external performance system owns KPIs. In this mode
    /// CRUD on the local catalog is locked — Index renders read-only with a banner
    /// pointing to the external system; Create/Edit/Retire/Restore POSTs are refused.</summary>
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
        var rows = await _db.KpiDefinitions
            .AsNoTracking()
            .Where(k => !k.IsDeleted)
            .OrderByDescending(k => k.IsActive)
            .ThenBy(k => k.Code)
            .ToListAsync();
        PopulateExternalModeViewBag();
        return View(rows);
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public IActionResult Create()
    {
        if (ExternalMode) return RedirectExternal();
        return View(new KpiDefinition());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Create(KpiDefinition model)
    {
        if (ExternalMode) return RedirectExternal();
        if (!ModelState.IsValid) return View(model);
        if (await _db.KpiDefinitions.AnyAsync(k => k.Code == model.Code && !k.IsDeleted))
        {
            ModelState.AddModelError(nameof(model.Code), "A KPI with this code already exists.");
            return View(model);
        }
        model.Id = Guid.NewGuid().ToString();
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        model.CreatedById = User.Identity?.Name;
        _db.KpiDefinitions.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"KPI '{model.Code}' added to catalog.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Edit(string id)
    {
        if (ExternalMode) return RedirectExternal();
        var kpi = await _db.KpiDefinitions.FindAsync(id);
        return kpi is null ? NotFound() : View(kpi);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Edit(string id, KpiDefinition model)
    {
        if (ExternalMode) return RedirectExternal();
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid) return View(model);

        var existing = await _db.KpiDefinitions.FindAsync(id);
        if (existing is null) return NotFound();
        if (existing.Code != model.Code &&
            await _db.KpiDefinitions.AnyAsync(k => k.Code == model.Code && k.Id != id && !k.IsDeleted))
        {
            ModelState.AddModelError(nameof(model.Code), "Another KPI with this code already exists.");
            return View(model);
        }

        existing.Code = model.Code;
        existing.NameEn = model.NameEn;
        existing.NameAr = model.NameAr;
        existing.DescriptionEn = model.DescriptionEn;
        existing.DescriptionAr = model.DescriptionAr;
        existing.UnitOfMeasure = model.UnitOfMeasure;
        existing.Direction = model.Direction;
        existing.DefaultType = model.DefaultType;
        existing.OwningUnitId = model.OwningUnitId;
        existing.IsActive = model.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedById = User.Identity?.Name;
        existing.Version++;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"KPI '{model.Code}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Retire(string id)
    {
        if (ExternalMode) return RedirectExternal();
        var kpi = await _db.KpiDefinitions.FindAsync(id);
        if (kpi is null) return NotFound();
        kpi.IsActive = false;
        kpi.UpdatedAt = DateTime.UtcNow;
        kpi.UpdatedById = User.Identity?.Name;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"KPI '{kpi.Code}' retired (historical references preserved).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    public async Task<IActionResult> Restore(string id)
    {
        if (ExternalMode) return RedirectExternal();
        var kpi = await _db.KpiDefinitions.FindAsync(id);
        if (kpi is null) return NotFound();
        kpi.IsActive = true;
        kpi.UpdatedAt = DateTime.UtcNow;
        kpi.UpdatedById = User.Identity?.Name;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"KPI '{kpi.Code}' restored.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectExternal()
    {
        TempData["Info"] = $"KPIs are managed in {_perf.ProviderName}. Local CRUD is disabled while the integration is active.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Lightweight typeahead — used by the Improvements wizard to attach a
    /// catalog KPI to a measurement row instead of free-text. Returns the
    /// top 10 active KPIs matching the query in code or English/Arabic name.
    /// Empty query returns the first 10 active KPIs (default suggestions).
    /// </summary>
    [HttpGet]
    [Authorize] // anyone authenticated can search the catalog (read-only); only Improvement.Edit can mutate
    public async Task<IActionResult> Search(string? q = null, int take = 10)
    {
        if (take <= 0 || take > 50) take = 10;
        var query = _db.KpiDefinitions
            .AsNoTracking()
            .Where(k => !k.IsDeleted && k.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            query = query.Where(k =>
                EF.Functions.Like(k.Code,   "%" + needle + "%") ||
                EF.Functions.Like(k.NameEn, "%" + needle + "%") ||
                EF.Functions.Like(k.NameAr, "%" + needle + "%"));
        }

        var rows = await query
            .OrderBy(k => k.Code)
            .Take(take)
            .Select(k => new
            {
                id = k.Id,
                code = k.Code,
                nameEn = k.NameEn,
                nameAr = k.NameAr,
                unit = k.UnitOfMeasure,
                direction = k.Direction.ToString(),
                defaultType = k.DefaultType.ToString()
            })
            .ToListAsync();
        return Json(rows);
    }
}
