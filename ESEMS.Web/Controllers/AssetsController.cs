using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Extensions;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Common;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for Asset Management (ISO 55001:2014)
/// Manages organizational assets, maintenance schedules, and lifecycle tracking
/// </summary>
[Authorize(Policy = AppPolicies.Module.Asset.View)]
public class AssetsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AssetsController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IScopingService _scopingService;

    public AssetsController(ApplicationDbContext context, ILogger<AssetsController> logger, IStringLocalizer<SharedResource> localizer, IScopingService scopingService)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _scopingService = scopingService;
    }

    public async Task<IActionResult> Index()
    {
        var query = _context.Assets
            .Where(a => !a.IsDeleted)
            .Include(a => a.Category)
            .Include(a => a.Process)
            .Include(a => a.AssignedToUnit)
            // IsMaintenanceDueSoon() in the view tile reads MaintenanceSchedules;
            // without this Include the nav is empty and the tile always shows 0.
            .Include(a => a.MaintenanceSchedules)
            .AsQueryable();

        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyAssignedUnitScope(scope);

        var assets = await query.OrderBy(a => a.AssetTag).ToListAsync();

        return View(assets);
    }

    public async Task<IActionResult> Details(string id)
    {
        var asset = await _context.Assets
            .Include(a => a.Category)
            .Include(a => a.Process)
            .Include(a => a.AssignedToUnit)
            // Real-estate parent — the housing-project Asset a villa/building rolls up to.
            // Loaded so the Details view can show the breadcrumb link without a separate fetch.
            .Include(a => a.ParentProject)
            .Include(a => a.MaintenanceRecords.OrderByDescending(m => m.PerformedDate))
            .Include(a => a.MaintenanceSchedules.Where(s => s.IsActive))
            .Include(a => a.AssetRisks)
                .ThenInclude(ar => ar.Risk)
                    .ThenInclude(r => r.Category)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        if (asset == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(asset))
            return NotFound();

        asset.CalculateCurrentValue();

        // F-RA-001: resolve Data Owner / Data Custodian names so the ISO 27001
        // info-asset register stops rendering raw user IDs ("194"). Fetched
        // only when at least one is set so we skip a round-trip on assets
        // that don't use the info-asset block.
        var ownerIds = new[] { asset.DataOwnerUserId, asset.DataCustodianUserId }
            .Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToArray();
        if (ownerIds.Length > 0)
        {
            ViewBag.UsersById = await _context.CustomUsers
                .Where(u => ownerIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName ?? u.EmployeeName ?? u.Username);
        }

        return View(asset);
    }

    [Authorize(Policy = AppPolicies.Module.Asset.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        var asset = new Asset
        {
            AssetTag = await GenerateAssetTag(),
            PurchaseDate = DateTime.UtcNow
        };
        return View(asset);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Asset asset)
    {
        if (ModelState.IsValid)
        {
            asset.Id = Guid.NewGuid().ToString();
            asset.AssetTag = await GenerateAssetTag();
            asset.CreatedAt = DateTime.UtcNow;
            asset.UpdatedAt = DateTime.UtcNow;
            asset.CalculateCurrentValue();

            _context.Assets.Add(asset);
            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_AssetCreated"].Value;
            return RedirectToAction(nameof(Details), new { id = asset.Id });
        }

        await PopulateDropdowns();
        return View(asset);
    }

    [Authorize(Policy = AppPolicies.Module.Asset.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var asset = await _context.Assets
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        if (asset == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(asset))
            return NotFound();

        await PopulateDropdowns();
        return View(asset);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Asset asset)
    {
        if (id != asset.Id)
            return NotFound();

        // IDOR mass-assignment defense: re-check scope on the *persisted*
        // AssignedToUnitId, not whatever the form posted.
        var existing = await _context.Assets.AsNoTracking()
            .Where(a => a.Id == id && !a.IsDeleted)
            .Select(a => new { a.AssignedToUnitId })
            .FirstOrDefaultAsync();
        if (existing == null) return NotFound();
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(new AssignedScopeProbe(existing.AssignedToUnitId)))
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                // FUNC-001: load-then-patch instead of _context.Update(boundEntity).
                // A model-bound entity lets a crafted POST forge IsDeleted /
                // CreatedById / CreatedAt / Version / DeletedAt. Load the tracked
                // row and copy ONLY the user-editable fields the Edit form posts;
                // AssetTag (auto-generated), the audit/system fields, and fields not
                // on the form (e.g. AssignedToUnitId — the scope FK) stay untouched.
                var tracked = await _context.Assets
                    .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
                if (tracked == null)
                    return NotFound();

                tracked.NameEn = asset.NameEn;
                tracked.NameAr = asset.NameAr;
                tracked.SerialNumber = asset.SerialNumber;
                tracked.CategoryId = asset.CategoryId;
                tracked.Manufacturer = asset.Manufacturer;
                tracked.Model = asset.Model;
                tracked.Status = asset.Status;
                tracked.Location = asset.Location;
                tracked.PurchaseDate = asset.PurchaseDate;
                tracked.PurchaseCost = asset.PurchaseCost;
                tracked.DepreciationRate = asset.DepreciationRate;
                tracked.WarrantyExpiryDate = asset.WarrantyExpiryDate;
                tracked.NextMaintenanceDate = asset.NextMaintenanceDate;
                tracked.Criticality = asset.Criticality;
                tracked.Notes = asset.Notes;
                // Real-estate fields
                tracked.PlotNumber = asset.PlotNumber;
                tracked.Emirate = asset.Emirate;
                tracked.District = asset.District;
                tracked.TitleDeedNumber = asset.TitleDeedNumber;
                tracked.BuiltUpAreaSqm = asset.BuiltUpAreaSqm;
                tracked.LandAreaSqm = asset.LandAreaSqm;
                tracked.Floors = asset.Floors;
                tracked.Units = asset.Units;
                tracked.Bedrooms = asset.Bedrooms;
                tracked.ConstructionStatus = asset.ConstructionStatus;
                tracked.GpsLatitude = asset.GpsLatitude;
                tracked.GpsLongitude = asset.GpsLongitude;
                // Information-asset (ISO 27001) fields
                tracked.Classification = asset.Classification;
                tracked.DataOwnerUserId = asset.DataOwnerUserId;
                tracked.DataCustodianUserId = asset.DataCustodianUserId;
                tracked.RetentionMonths = asset.RetentionMonths;
                tracked.RegulatoryTags = asset.RegulatoryTags;
                tracked.StorageSystem = asset.StorageSystem;
                tracked.DataFormat = asset.DataFormat;
                tracked.RecordCount = asset.RecordCount;

                tracked.UpdatedAt = DateTime.UtcNow;
                tracked.UpdatedById = User.Identity?.Name;
                tracked.Version++;
                tracked.CalculateCurrentValue();
                await _context.SaveChangesAsync();

                TempData["Success"] = _localizer["Success_AssetUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id = tracked.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await AssetExists(asset.Id))
                    return NotFound();
                throw;
            }
        }

        await PopulateDropdowns();
        return View(asset);
    }

    /// <summary>
    /// Assets Dashboard with KPIs, charts, and analytics
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        // SCOPE: dashboard KPIs must respect the user's data scope, same as Index
        // — otherwise a scoped user sees org-wide asset counts + total value.
        // No-op for All-scope users.
        var scope = await _scopingService.GetScopeAsync(User);
        var assets = await _context.Assets
            .Where(a => !a.IsDeleted)
            .ApplyAssignedUnitScope(scope)
            .Include(a => a.Category)
            .Include(a => a.MaintenanceSchedules)
            .ToListAsync();

        // Calculate current values
        foreach (var asset in assets)
        {
            asset.CalculateCurrentValue();
        }

        // KPIs
        ViewBag.TotalAssets = assets.Count;
        ViewBag.OperationalAssets = assets.Count(a => a.Status == AssetStatus.Operational);
        ViewBag.UnderMaintenanceAssets = assets.Count(a => a.Status == AssetStatus.UnderMaintenance);
        ViewBag.TotalValue = assets.Sum(a => a.CurrentValue);

        // Alerts
        var today = DateTime.UtcNow.Date;
        ViewBag.MaintenanceDue = assets.Count(a => a.IsMaintenanceDueSoon(30));
        ViewBag.WarrantyExpiring = assets.Count(a => a.WarrantyExpiryDate.HasValue && a.WarrantyExpiryDate.Value <= today.AddDays(90) && a.WarrantyExpiryDate.Value >= today);

        return View(assets);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var asset = await _context.Assets.FindAsync(id);
        if (asset != null)
        {
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(asset))
                return NotFound();

            // FUNC-003: in-use guard. Soft-deleting an asset that's still linked
            // to services, risks, change requests, maintenance, child assets, or
            // incidents/problems would orphan those rows (dangling FK targets on
            // related dashboards). Refuse if anything still references it.
            var serviceAssetCount = await _context.ServiceAssets
                .CountAsync(sa => sa.AssetId == id);
            var assetRiskCount = await _context.AssetRisks
                .CountAsync(ar => ar.AssetId == id);
            var changeRequestAssetCount = await _context.ChangeRequestAssets
                .CountAsync(cra => cra.AssetId == id);
            var maintenanceScheduleCount = await _context.MaintenanceSchedules
                .CountAsync(ms => ms.AssetId == id);
            var maintenanceRecordCount = await _context.MaintenanceRecords
                .CountAsync(mr => mr.AssetId == id);
            var childAssetCount = await _context.Assets
                .CountAsync(a => a.ParentProjectId == id && !a.IsDeleted);
            var incidentCount = await _context.Incidents
                .CountAsync(i => i.AssetId == id && !i.IsDeleted);
            var problemCount = await _context.Problems
                .CountAsync(p => p.AssetId == id && !p.IsDeleted);

            var inUse = serviceAssetCount + assetRiskCount + changeRequestAssetCount
                      + maintenanceScheduleCount + maintenanceRecordCount
                      + childAssetCount + incidentCount + problemCount;
            if (inUse > 0)
            {
                var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
                TempData["Error"] = isArabic
                    ? $"لا يمكن الحذف: ما زال هذا الأصل مرتبطاً بـ {serviceAssetCount} خدمات، {assetRiskCount} مخاطر، {changeRequestAssetCount} طلبات تغيير، {maintenanceScheduleCount} جداول صيانة، {maintenanceRecordCount} سجلات صيانة، {childAssetCount} أصول فرعية، {incidentCount} حوادث، {problemCount} مشكلات. أزل تلك الروابط أولاً."
                    : $"Cannot delete: this asset is still linked to {serviceAssetCount} services, {assetRiskCount} risks, {changeRequestAssetCount} change requests, {maintenanceScheduleCount} maintenance schedules, {maintenanceRecordCount} maintenance records, {childAssetCount} child assets, {incidentCount} incidents, and {problemCount} problems. Remove those links first.";
                return RedirectToAction(nameof(Index));
            }

            asset.IsDeleted = true;
            asset.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = _localizer["Success_AssetDeleted"].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    private sealed record AssignedScopeProbe(int? AssignedToUnitId)
        : ESEMS.Web.Models.Common.IAssignedToUnit;

    /// <summary>
    /// F-008: record-level scope (IDOR) gate for the asset-risk link/available
    /// helper actions, which only have an assetId in hand. Resolves the asset's
    /// AssignedToUnitId and checks it against the caller's scope. Returns true
    /// when the asset is missing too — callers already handle the not-found case
    /// with their own message, and we don't want to leak existence via a
    /// different code path. Returns false only when the asset exists but is out
    /// of scope. Mirrors ServicesController.CanAccessServiceAsync.
    /// </summary>
    private async Task<bool> CanAccessAssetAsync(string assetId)
    {
        var owner = await _context.Assets.AsNoTracking()
            .Where(a => a.Id == assetId && !a.IsDeleted)
            .Select(a => new { a.AssignedToUnitId })
            .FirstOrDefaultAsync();
        if (owner == null) return true; // missing — let the caller's own null check answer
        var scope = await _scopingService.GetScopeAsync(User);
        return scope.CanAccess(new AssignedScopeProbe(owner.AssignedToUnitId));
    }

    /// <summary>
    /// F-008: record-level scope (IDOR) gate for <see cref="GetAvailableAssets"/>,
    /// which is keyed by a riskId rather than an assetId. Resolves the
    /// EnterpriseRisk's OrganizationUnitId (IOrganizationScoped) and checks it
    /// against the caller's scope, so an out-of-scope risk id can't be used to
    /// enumerate the asset catalog. Returns true when the risk is missing so the
    /// caller's own handling applies.
    /// </summary>
    private async Task<bool> CanAccessRiskAsync(string riskId)
    {
        var owner = await _context.EnterpriseRisks.AsNoTracking()
            .Where(r => r.Id == riskId && !r.IsDeleted)
            .Select(r => new { r.OrganizationUnitId })
            .FirstOrDefaultAsync();
        if (owner == null) return true; // missing — let the caller's own null check answer
        var scope = await _scopingService.GetScopeAsync(User);
        return scope.CanAccess(new RiskScopeProbe(owner.OrganizationUnitId));
    }

    private sealed record RiskScopeProbe(int? OrganizationUnitId)
        : ESEMS.Web.Models.Common.IOrganizationScoped;

    private async Task PopulateDropdowns()
    {
        ViewBag.Categories = new SelectList(await _context.AssetCategories.Where(c => !c.IsDeleted).ToListAsync(), "Id", "Name");
        ViewBag.Processes = new SelectList(await _context.Processes.Where(p => !p.IsDeleted).OrderBy(p => p.Code).ToListAsync(), "Id", "Name");
        ViewBag.OrganizationUnits = new SelectList(await _context.OrganizationUnits.Where(o => !o.IsDeleted).ToListAsync(), "Id", "Name");
        // F-RA-001/007: Data Owner & Custodian pickers — used by ISO 27001
        // info-asset register. CustomUser.IsActive is [NotMapped] so EF can't
        // translate it; the legacy [user] table has no active/inactive flag,
        // so we just project the three name columns and do null-coalescing +
        // ordering client-side after materialization. (Regression fix for
        // F-D-001 caught by the 2026-05-20 dynamic audit.)
        var raw = await _context.CustomUsers
            .Select(u => new { u.UserId, u.FullName, u.EmployeeName, u.Username })
            .ToListAsync();
        var users = raw
            .Select(u => new { u.UserId, Name = u.FullName ?? u.EmployeeName ?? u.Username })
            .OrderBy(u => u.Name)
            .ToList();
        ViewBag.Users = new SelectList(users, "UserId", "Name");
    }

    private async Task<string> GenerateAssetTag()
    {
        var year = DateTime.UtcNow.Year;
        var lastAsset = await _context.Assets
            .Where(a => a.AssetTag.StartsWith($"AST-{year}-"))
            .OrderByDescending(a => a.AssetTag)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastAsset != null)
        {
            var parts = lastAsset.AssetTag.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"AST-{year}-{nextNumber:D4}";
    }

    private async Task<bool> AssetExists(string id)
    {
        return await _context.Assets.AnyAsync(a => a.Id == id && !a.IsDeleted);
    }

    #region Asset-Risk Relationship Management

    /// <summary>
    /// Link a risk to an asset
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Asset.Edit)]
    public async Task<IActionResult> LinkRisk(string assetId, string riskId, int impactLevel = 3, string? specificControls = null, string? notes = null)
    {
        try
        {
            // Validate asset exists
            var asset = await _context.Assets.FindAsync(assetId);
            if (asset == null || asset.IsDeleted)
                return Json(new { success = false, message = _localizer["Error_AssetNotFound"].Value });

            // F-008: record-level scope (IDOR) on the owning asset.
            if (!await CanAccessAssetAsync(assetId))
                return Json(new { success = false, message = _localizer["Error_AssetNotFound"].Value });

            // Validate risk exists
            var risk = await _context.EnterpriseRisks.FindAsync(riskId);
            if (risk == null || risk.IsDeleted)
                return Json(new { success = false, message = _localizer["Error_RiskNotFound"].Value });

            // Check if relationship already exists
            var existing = await _context.Set<AssetRisk>()
                .FirstOrDefaultAsync(ar => ar.AssetId == assetId && ar.RiskId == riskId);

            if (existing != null)
                return Json(new { success = false, message = "This risk is already linked to this asset." });

            // Create relationship
            var assetRisk = new AssetRisk
            {
                AssetId = assetId,
                RiskId = riskId,
                ImpactLevel = impactLevel,
                SpecificControls = specificControls,
                Notes = notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedById = User.Identity?.Name,
                UpdatedById = User.Identity?.Name
            };

            _context.Set<AssetRisk>().Add(assetRisk);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Risk {RiskId} linked to Asset {AssetId} by {User}", riskId, assetId, User.Identity?.Name);

            return Json(new { success = true, message = "Risk linked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking risk {RiskId} to asset {AssetId}", riskId, assetId);
            return Json(new { success = false, message = "An error occurred while linking the risk." });
        }
    }

    /// <summary>
    /// Unlink a risk from an asset
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Asset.Edit)]
    public async Task<IActionResult> UnlinkRisk(string assetId, string riskId)
    {
        try
        {
            // F-008: record-level scope (IDOR) on the owning asset.
            if (!await CanAccessAssetAsync(assetId))
                return Json(new { success = false, message = "Risk relationship not found." });

            var assetRisk = await _context.Set<AssetRisk>()
                .FirstOrDefaultAsync(ar => ar.AssetId == assetId && ar.RiskId == riskId);

            if (assetRisk == null)
                return Json(new { success = false, message = "Risk relationship not found." });

            _context.Set<AssetRisk>().Remove(assetRisk);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Risk {RiskId} unlinked from Asset {AssetId} by {User}", riskId, assetId, User.Identity?.Name);

            return Json(new { success = true, message = "Risk unlinked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking risk {RiskId} from asset {AssetId}", riskId, assetId);
            return Json(new { success = false, message = "An error occurred while unlinking the risk." });
        }
    }

    /// <summary>
    /// Update asset-risk relationship details
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Asset.Edit)]
    public async Task<IActionResult> UpdateAssetRisk(string assetId, string riskId, int impactLevel, string? specificControls = null, string? notes = null)
    {
        try
        {
            // F-008: record-level scope (IDOR) on the owning asset.
            if (!await CanAccessAssetAsync(assetId))
                return Json(new { success = false, message = "Risk relationship not found." });

            var assetRisk = await _context.Set<AssetRisk>()
                .FirstOrDefaultAsync(ar => ar.AssetId == assetId && ar.RiskId == riskId);

            if (assetRisk == null)
                return Json(new { success = false, message = "Risk relationship not found." });

            assetRisk.ImpactLevel = impactLevel;
            assetRisk.SpecificControls = specificControls;
            assetRisk.Notes = notes;
            assetRisk.UpdatedAt = DateTime.UtcNow;
            assetRisk.UpdatedById = User.Identity?.Name;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Asset-Risk relationship updated for Asset {AssetId} and Risk {RiskId} by {User}", assetId, riskId, User.Identity?.Name);

            return Json(new { success = true, message = "Relationship updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset-risk relationship for Asset {AssetId} and Risk {RiskId}", assetId, riskId);
            return Json(new { success = false, message = "An error occurred while updating the relationship." });
        }
    }

    /// <summary>
    /// Get available risks for linking (not already linked to this asset)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableRisks(string assetId)
    {
        try
        {
            // F-008: record-level scope (IDOR) on the owning asset — return an
            // empty list (don't leak risks) for an out-of-scope asset.
            if (!await CanAccessAssetAsync(assetId))
                return Json(new List<object>());

            var linkedRiskIds = await _context.Set<AssetRisk>()
                .Where(ar => ar.AssetId == assetId)
                .Select(ar => ar.RiskId)
                .ToListAsync();

            var availableRisks = await _context.EnterpriseRisks
                .Where(r => !r.IsDeleted && r.IsActive && !linkedRiskIds.Contains(r.Id))
                .Include(r => r.Category)
                .OrderBy(r => r.RiskNumber)
                .Select(r => new
                {
                    r.Id,
                    r.RiskNumber,
                    r.NameEn,
                    r.NameAr,
                    CategoryName = r.Category != null ? r.Category.NameEn : "",
                    r.RiskLevel,
                    r.InherentRiskScore
                })
                .ToListAsync();

            return Json(availableRisks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available risks for asset {AssetId}", assetId);
            return Json(new List<object>());
        }
    }

    /// <summary>
    /// Get available assets for linking to a risk (not already linked to this risk)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableAssets(string riskId)
    {
        try
        {
            // F-008: record-level scope (IDOR) on the owning risk — return an
            // empty list (don't leak assets) for an out-of-scope risk.
            if (!await CanAccessRiskAsync(riskId))
                return Json(new List<object>());

            var linkedAssetIds = await _context.Set<AssetRisk>()
                .Where(ar => ar.RiskId == riskId)
                .Select(ar => ar.AssetId)
                .ToListAsync();

            var scope = await _scopingService.GetScopeAsync(User);
            var availableAssets = await _context.Assets
                .Where(a => !linkedAssetIds.Contains(a.Id))
                .ApplyAssignedUnitScope(scope)
                .Include(a => a.Category)
                .OrderBy(a => a.AssetTag)
                .Select(a => new
                {
                    a.Id,
                    a.AssetTag,
                    a.NameEn,
                    a.NameAr,
                    CategoryName = a.Category != null ? a.Category.NameEn : "",
                    a.Status,
                    a.Criticality
                })
                .ToListAsync();

            return Json(availableAssets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available assets for risk {RiskId}", riskId);
            return Json(new List<object>());
        }
    }

    #endregion
}
