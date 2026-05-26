using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.RiskManagement;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Extensions;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Common;
using ESEMS.Web.Services.Integrations.Contracts;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for Enterprise Risk Management (ISO 31000:2018)
/// Manages organizational risks, assessments, and mitigation strategies
/// </summary>
[Authorize(Policy = AppPolicies.Module.Risk.View)]
public class EnterpriseRisksController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EnterpriseRisksController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IScopingService _scopingService;
    private readonly IRiskProvider _riskProvider;

    public EnterpriseRisksController(
        ApplicationDbContext context,
        ILogger<EnterpriseRisksController> logger,
        IStringLocalizer<SharedResource> localizer,
        IScopingService scopingService,
        IRiskProvider riskProvider)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _scopingService = scopingService;
        _riskProvider = riskProvider;
    }

    /// <summary>
    /// True when an external risk system owns the source of truth. In this mode the
    /// local risk store is bypassed for listing/creating/editing — the user is shown
    /// risks from the external system and Add/Edit buttons deep-link out to it. Local
    /// records remain readable on Details for legacy data, but nothing new is written
    /// here.
    /// </summary>
    private bool ExternalMode => _riskProvider.IsEnabled;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // ── External-mode short-circuit ──────────────────────────────────
        // When the risk system integration is on, ESEMS does not own the risks.
        // Pull from the external API and render the same Index view in proxy mode.
        if (ExternalMode)
        {
            var externalRisks = await _riskProvider.GetAllRisksAsync(take: 200, ct);
            ViewBag.ExternalMode         = true;
            ViewBag.ExternalRisks        = externalRisks;
            ViewBag.ExternalProviderName = _riskProvider.ProviderName;
            ViewBag.ExternalAddRiskUrl   = _riskProvider.GetCreateUrl();
            ViewBag.ExternalIndexUrl     = _riskProvider.GetIndexUrl();
            return View(Enumerable.Empty<EnterpriseRisk>());
        }

        // ── Local-mode (no integration configured) ───────────────────────
        var query = _context.EnterpriseRisks
            .Where(r => !r.IsDeleted && r.IsActive)
            .Include(r => r.Category)
            .Include(r => r.Process)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.ActionPlans)
            .AsQueryable();

        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyOrganizationScope(scope);

        var risks = await query.OrderByDescending(r => r.InherentRiskScore).ToListAsync();

        // Recompute scores so RiskLevel-driven KPIs in the view reflect the
        // current Likelihood × Impact, matching the Dashboard's defensive
        // recalc. Without this, stale stored RiskLevel can disagree with the
        // Dashboard's tier counts and present contradictory KPIs to the user.
        foreach (var risk in risks)
        {
            risk.CalculateInherentRiskScore();
            risk.CalculateResidualRiskScore();
        }

        ViewBag.ExternalMode = false;
        return View(risks);
    }

    public async Task<IActionResult> Details(string id)
    {
        var risk = await _context.EnterpriseRisks
            .Include(r => r.Category)
            .Include(r => r.Process)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.ActionPlans)
            .Include(r => r.AssetRisks)
                .ThenInclude(ar => ar.Asset)
                    .ThenInclude(a => a.Category)
            .Include(r => r.ImprovementRisks)
                .ThenInclude(ir => ir.Improvement)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (risk == null)
            return NotFound();

        // Per-record IDOR guard: scoped users must not reach records outside
        // their visible org-unit subtree by URL tampering. NotFound (not Forbid)
        // intentionally — don't leak record existence to a user who can't see it.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(risk))
            return NotFound();

        risk.CalculateInherentRiskScore();
        risk.CalculateResidualRiskScore();

        return View(risk);
    }

    [Authorize(Policy = AppPolicies.Module.Risk.Create)]
    public async Task<IActionResult> Create()
    {
        // External mode: don't let the user create a local record. Bounce them to the
        // external system's "New Risk" page if it exposes one; otherwise back to Index
        // with a message so they can use the Add button there (which has the same logic).
        if (ExternalMode)
        {
            var url = _riskProvider.GetCreateUrl();
            if (!string.IsNullOrWhiteSpace(url))
                return Redirect(url);
            TempData["Error"] = $"Risks are managed in {_riskProvider.ProviderName}. Open that system to create a new risk.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns();
        var risk = new EnterpriseRisk
        {
            RiskNumber = await GenerateRiskNumber()
        };
        risk.CalculateInherentRiskScore();
        return View(risk);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Risk.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EnterpriseRisk risk)
    {
        // Defence in depth: if integration is on, refuse the POST too. The Create GET
        // already redirects, but an admin who flipped the toggle while a user had the
        // form open could otherwise still submit it.
        if (ExternalMode)
        {
            TempData["Error"] = $"Risks are managed in {_riskProvider.ProviderName}. The local create form is disabled.";
            return RedirectToAction(nameof(Index));
        }

        if (ModelState.IsValid)
        {
            risk.Id = Guid.NewGuid().ToString();
            risk.RiskNumber = await GenerateRiskNumber();
            risk.CreatedAt = DateTime.UtcNow;
            risk.UpdatedAt = DateTime.UtcNow;
            risk.CalculateInherentRiskScore();
            risk.CalculateResidualRiskScore();

            _context.EnterpriseRisks.Add(risk);
            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_RiskCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = risk.Id });
        }

        await PopulateDropdowns();
        return View(risk);
    }

    [Authorize(Policy = AppPolicies.Module.Risk.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        // External mode: don't allow local edits — even legacy local records are
        // read-only in this mode (the user's mental model is "risks live in
        // {ProviderName}", and editing one in two places is a recipe for divergence).
        if (ExternalMode)
        {
            TempData["Error"] = $"Risks are managed in {_riskProvider.ProviderName}. Local records are read-only.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var risk = await _context.EnterpriseRisks
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (risk == null)
            return NotFound();

        // IDOR guard — see Details. Editing a risk outside your scope is a
        // hard no even if you carry Risk.Edit permission.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(risk))
            return NotFound();

        await PopulateDropdowns();
        return View(risk);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Risk.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, EnterpriseRisk risk)
    {
        // Same defence in depth as Create POST.
        if (ExternalMode)
        {
            TempData["Error"] = $"Risks are managed in {_riskProvider.ProviderName}. Local records are read-only.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (id != risk.Id)
            return NotFound();

        // IDOR guard on POST — re-check the *persisted* OrganizationUnitId,
        // not whatever the user posted in the form (mass-assignment defense).
        var existing = await _context.EnterpriseRisks
            .AsNoTracking()
            .Where(r => r.Id == id && !r.IsDeleted)
            .Select(r => new { r.OrganizationUnitId })
            .FirstOrDefaultAsync();
        if (existing == null)
            return NotFound();
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(new ScopeProbe(existing.OrganizationUnitId)))
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                // FUNC-001: load-then-patch instead of _context.Update(boundEntity).
                // A model-bound entity lets a crafted POST forge IsDeleted /
                // CreatedById / CreatedAt / Version / DeletedAt. Load the tracked
                // row and copy ONLY the user-editable fields the Edit form posts;
                // RiskNumber stays auto-generated, fields not on the form (e.g.
                // OrganizationUnitId — the scope FK) stay untouched, and the
                // scores/level are recomputed below.
                var tracked = await _context.EnterpriseRisks
                    .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
                if (tracked == null)
                    return NotFound();

                tracked.NameEn = risk.NameEn;
                tracked.NameAr = risk.NameAr;
                tracked.CategoryId = risk.CategoryId;
                tracked.Likelihood = risk.Likelihood;
                tracked.Impact = risk.Impact;
                tracked.ResidualLikelihood = risk.ResidualLikelihood;
                tracked.ResidualImpact = risk.ResidualImpact;
                tracked.ToleranceLevel = risk.ToleranceLevel;
                tracked.CurrentControls = risk.CurrentControls;
                tracked.ControlEffectiveness = risk.ControlEffectiveness;
                tracked.ResponseStrategy = risk.ResponseStrategy;
                tracked.NextReviewDate = risk.NextReviewDate;
                tracked.IsActive = risk.IsActive;

                tracked.UpdatedAt = DateTime.UtcNow;
                tracked.UpdatedById = User.Identity?.Name;
                tracked.Version++;
                // FUNC-007: recompute scores/level from the patched Likelihood ×
                // Impact so stored InherentRiskScore / ResidualRiskScore / RiskLevel
                // can't be forged via the POST and always agree with the inputs.
                tracked.CalculateInherentRiskScore();
                tracked.CalculateResidualRiskScore();
                await _context.SaveChangesAsync();

                TempData["Success"] = _localizer["Success_RiskUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id = tracked.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await RiskExists(risk.Id))
                    return NotFound();
                throw;
            }
        }

        await PopulateDropdowns();
        return View(risk);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Risk.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        // External mode: block local soft-delete. The external system is the source of
        // truth; deleting locally would just orphan the record on the next overlay.
        if (ExternalMode)
        {
            TempData["Error"] = $"Risks are managed in {_riskProvider.ProviderName}. Use that system to delete.";
            return RedirectToAction(nameof(Index));
        }

        var risk = await _context.EnterpriseRisks.FindAsync(id);
        if (risk != null)
        {
            // IDOR guard — even with Risk.Delete, scoped users can't soft-delete
            // records outside their unit subtree.
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(risk))
                return NotFound();

            // In-use guard: refuse if action plans or asset-risk links still
            // point at this risk. Soft-deleting the parent would orphan them
            // (they still reference a now-hidden FK target and surface as
            // dangling rows on related dashboards).
            var actionPlanCount = await _context.RiskActionPlans
                .CountAsync(p => p.RiskId == id && !p.IsDeleted);
            var assetRiskCount = await _context.AssetRisks
                .CountAsync(ar => ar.RiskId == id);
            if (actionPlanCount > 0 || assetRiskCount > 0)
            {
                TempData["Error"] = string.Format(
                    _localizer["Error_RiskInUse"].Value,
                    actionPlanCount, assetRiskCount);
                return RedirectToAction(nameof(Index));
            }

            risk.IsDeleted = true;
            risk.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = _localizer["Success_RiskDeleted"].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Lightweight adapter so the IDOR guard on the POST Edit can check the
    /// persisted OrganizationUnitId without re-loading the full entity.
    /// </summary>
    private sealed record ScopeProbe(int? OrganizationUnitId) : ESEMS.Web.Models.Common.IOrganizationScoped;

    /// <summary>
    /// Risks Dashboard with KPIs, charts, and analytics. In external mode the
    /// charts can't be computed (no local likelihood × impact matrix) so we redirect
    /// to the external system's UI if it has one, else back to the proxied Index.
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        if (ExternalMode)
        {
            var url = _riskProvider.GetIndexUrl();
            if (!string.IsNullOrWhiteSpace(url))
                return Redirect(url);
            TempData["Error"] = $"Risk dashboards are managed in {_riskProvider.ProviderName}.";
            return RedirectToAction(nameof(Index));
        }
        return await DashboardLocal();
    }

    private async Task<IActionResult> DashboardLocal()
    {
        // Apply the same organization scope used by Index so the Dashboard
        // KPIs ("Critical risks", "High risks") line up with what the user
        // sees on the list. Without this filter the Dashboard would count
        // risks the user can't open on Index — a hard inconsistency.
        var scope = await _scopingService.GetScopeAsync(User);
        var risks = await _context.EnterpriseRisks
            .Where(r => !r.IsDeleted && r.IsActive)
            .Include(r => r.Category)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.ActionPlans)
            .ApplyOrganizationScope(scope)
            .ToListAsync();

        // Calculate risk scores
        foreach (var risk in risks)
        {
            risk.CalculateInherentRiskScore();
            risk.CalculateResidualRiskScore();
        }

        // KPIs
        ViewBag.TotalRisks = risks.Count;
        ViewBag.CriticalRisks = risks.Count(r => r.RiskLevel == RiskLevel.Critical);
        ViewBag.HighRisks = risks.Count(r => r.RiskLevel == RiskLevel.High);
        ViewBag.OutsideTolerance = risks.Count(r => r.ToleranceLevel.HasValue && r.RiskLevel > r.ToleranceLevel.Value);

        // Alerts
        var today = DateTime.UtcNow.Date;
        ViewBag.ReviewDue = risks.Count(r => r.NextReviewDate.HasValue && r.NextReviewDate.Value <= today.AddDays(30));
        ViewBag.NoMitigation = risks.Count(r => !r.ActionPlans.Any(ap => !ap.IsCompleted()));

        return View(risks);
    }

    /// <summary>
    /// Interactive Risk Heat Map view with filtering capabilities. Same external-mode
    /// rule as Dashboard — heat-map cells need likelihood × impact, which the external
    /// DTO doesn't carry. Defer to the external system if it has its own heat map.
    /// </summary>
    public async Task<IActionResult> HeatMap()
    {
        if (ExternalMode)
        {
            var url = _riskProvider.GetIndexUrl();
            if (!string.IsNullOrWhiteSpace(url))
                return Redirect(url);
            TempData["Error"] = $"Risk heat maps are managed in {_riskProvider.ProviderName}.";
            return RedirectToAction(nameof(Index));
        }

        var query = _context.EnterpriseRisks
            .Where(r => !r.IsDeleted && r.IsActive)
            .Include(r => r.Category)
            .Include(r => r.Process)
            .Include(r => r.OrganizationUnit)
            .AsQueryable();

        // Heat Map was previously unscoped — every user saw every risk regardless
        // of their org-unit assignment, leaking risks from other units. Mirror
        // Index/Dashboard scoping.
        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyOrganizationScope(scope);

        var risks = await query.OrderByDescending(r => r.InherentRiskScore).ToListAsync();

        // Recompute scores so the KPI band counts match Index/Dashboard. Without
        // this, stale stored RiskLevel can show different tier counts than the
        // other two views.
        foreach (var risk in risks)
        {
            risk.CalculateInherentRiskScore();
            risk.CalculateResidualRiskScore();
        }

        return View(risks);
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Categories = new SelectList(await _context.RiskCategories.Where(c => !c.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.Processes = new SelectList(await _context.Processes.Where(p => !p.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.OrganizationUnits = new SelectList(await _context.OrganizationUnits.Where(o => !o.IsDeleted).ToListAsync(), "Id", "Name");
    }

    private async Task<string> GenerateRiskNumber()
    {
        var year = DateTime.UtcNow.Year;
        var lastRisk = await _context.EnterpriseRisks
            .Where(r => r.RiskNumber.StartsWith($"RISK-{year}-"))
            .OrderByDescending(r => r.RiskNumber)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastRisk != null)
        {
            var parts = lastRisk.RiskNumber.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"RISK-{year}-{nextNumber:D4}";
    }

    private async Task<bool> RiskExists(string id)
    {
        return await _context.EnterpriseRisks.AnyAsync(r => r.Id == id && !r.IsDeleted);
    }
}
