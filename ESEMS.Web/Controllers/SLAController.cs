using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.SLA;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for SLA Management (ISO 20000-1:2018)
/// Manages service level agreements and breach monitoring
/// </summary>
[Authorize(Policy = AppPolicies.Module.Service.View)]
public class SLAController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SLAController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SLAController(ApplicationDbContext context, ILogger<SLAController> logger, IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index()
    {
        var slas = await _context.SLADefinitions
            .Where(s => !s.IsDeleted && s.IsActive)
            .Include(s => s.Service)
            .Include(s => s.ResponsibleUnit)
            .Include(s => s.Breaches)
            .OrderBy(s => s.Code)
            .ToListAsync();

        return View(slas);
    }

    public async Task<IActionResult> Details(string id)
    {
        var sla = await _context.SLADefinitions
            .Include(s => s.Service)
            .Include(s => s.ResponsibleUnit)
            .Include(s => s.Breaches.OrderByDescending(b => b.BreachDate))
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        if (sla == null)
            return NotFound();

        return View(sla);
    }

    [Authorize(Policy = AppPolicies.Module.Service.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        var sla = new SLADefinition
        {
            Code = await GenerateSLACode(),
            EffectiveFrom = DateTime.UtcNow
        };
        return View(sla);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Service.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SLADefinition sla)
    {
        if (ModelState.IsValid)
        {
            sla.Id = Guid.NewGuid().ToString();
            sla.Code = await GenerateSLACode();

            _context.SLADefinitions.Add(sla);
            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_SLACreated"].Value;
            return RedirectToAction(nameof(Details), new { id = sla.Id });
        }

        await PopulateDropdowns();
        return View(sla);
    }

    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var sla = await _context.SLADefinitions
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        if (sla == null)
            return NotFound();

        await PopulateDropdowns();
        return View(sla);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, SLADefinition sla)
    {
        if (id != sla.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            // FUNC-001 mass-assignment defense: load the tracked entity and copy
            // only user-editable fields. _context.Update(sla) wrote every column
            // from the post, letting a crafted body flip IsDeleted=true or forge
            // CreatedById/CreatedAt/Version/DeletedAt. Code is server-generated
            // and is never read back from the form.
            var existing = await _context.SLADefinitions
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (existing == null)
                return NotFound();

            existing.NameEn               = sla.NameEn;
            existing.NameAr               = sla.NameAr;
            existing.DescriptionEn        = sla.DescriptionEn;
            existing.DescriptionAr        = sla.DescriptionAr;
            existing.ServiceId            = sla.ServiceId;
            existing.ResponsibleUnitId    = sla.ResponsibleUnitId;
            existing.MetricName           = sla.MetricName;
            existing.TargetValue          = sla.TargetValue;
            existing.Unit                 = sla.Unit;
            existing.WarningThreshold     = sla.WarningThreshold;
            existing.MeasurementFrequency = sla.MeasurementFrequency;
            existing.CalculationMethod    = sla.CalculationMethod;
            existing.EffectiveFrom        = sla.EffectiveFrom;
            existing.EffectiveTo          = sla.EffectiveTo;
            existing.IsActive             = sla.IsActive;
            existing.PenaltyForBreach     = sla.PenaltyForBreach;
            existing.EscalationProcedure  = sla.EscalationProcedure;
            existing.UpdatedAt            = DateTime.UtcNow;
            existing.UpdatedById          = User.Identity?.Name;

            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_SLAUpdated"].Value;
            return RedirectToAction(nameof(Details), new { id = existing.Id });
        }

        await PopulateDropdowns();
        return View(sla);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Service.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var sla = await _context.SLADefinitions.FindAsync(id);
        if (sla != null)
        {
            sla.IsDeleted = true;
            sla.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = _localizer["Success_SLADeleted"].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Breaches()
    {
        var breaches = await _context.SLABreaches
            .Include(b => b.SLADefinition)
                .ThenInclude(s => s.Service)
            .Include(b => b.Incident)
            .OrderByDescending(b => b.BreachDate)
            .Take(100)
            .ToListAsync();

        return View(breaches);
    }

    /// <summary>
    /// SLA Compliance Dashboard with real-time monitoring
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        var slas = await _context.SLADefinitions
            .Where(s => !s.IsDeleted && s.IsActive)
            .Include(s => s.Service)
            .Include(s => s.Breaches.Where(b => b.BreachDate >= DateTime.UtcNow.AddMonths(-3)))
            .OrderBy(s => s.Code)
            .ToListAsync();

        var breaches = await _context.SLABreaches
            .Where(b => b.BreachDate >= DateTime.UtcNow.AddMonths(-3))
            .Include(b => b.SLADefinition)
            .OrderByDescending(b => b.BreachDate)
            .Take(20)
            .ToListAsync();

        ViewBag.RecentBreaches = breaches;
        ViewBag.TotalBreaches = await _context.SLABreaches.CountAsync();
        ViewBag.ActiveBreaches = await _context.SLABreaches.CountAsync(b => !b.IsResolved);
        ViewBag.ResolvedBreaches = await _context.SLABreaches.CountAsync(b => b.IsResolved);

        return View(slas);
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Services = new SelectList(await _context.Services.Where(s => !s.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.OrganizationUnits = new SelectList(await _context.OrganizationUnits.Where(o => !o.IsDeleted).ToListAsync(), "Id", "Name");
    }

    private async Task<string> GenerateSLACode()
    {
        var year = DateTime.UtcNow.Year;
        var lastSLA = await _context.SLADefinitions
            .Where(s => s.Code.StartsWith($"SLA-{year}-"))
            .OrderByDescending(s => s.Code)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastSLA != null)
        {
            var parts = lastSLA.Code.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"SLA-{year}-{nextNumber:D4}";
    }
}

