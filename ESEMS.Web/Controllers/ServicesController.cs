using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.ServiceManagement;
using ESEMS.Web.Extensions;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Common;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for Services management
/// </summary>
[Authorize(Policy = AppPolicies.Module.Service.View)]
public class ServicesController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServicesController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IScopingService _scopingService;
    private readonly HierarchicalCodeService _codeSvc;

    public ServicesController(ApplicationDbContext context, ILogger<ServicesController> logger, IStringLocalizer<SharedResource> localizer, IScopingService scopingService, HierarchicalCodeService codeSvc)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _scopingService = scopingService;
        _codeSvc = codeSvc;
    }

    /// <summary>
    /// Service Management Dashboard/Overview
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        // SCOPE: dashboard KPIs + recent list must respect the user's data scope,
        // same as Index. Services scope by OwningUnit; Incidents/Problems by
        // AssignedToUnit. No-op for All-scope users.
        var scope = await _scopingService.GetScopeAsync(User);
        var svcScoped = _context.Services.Where(s => !s.IsDeleted).ApplyOwningUnitScope(scope);
        var incScoped = _context.Incidents.Where(i => !i.IsDeleted).ApplyAssignedUnitScope(scope);
        var probScoped = _context.Problems.Where(p => !p.IsDeleted).ApplyAssignedUnitScope(scope);

        var totalServices = await svcScoped.CountAsync();
        var internalServices = await svcScoped.CountAsync(s => s.ServiceType == ServiceType.Internal);
        var externalServices = await svcScoped.CountAsync(s => s.ServiceType == ServiceType.External);
        var totalIncidents = await incScoped.CountAsync();
        var openIncidents = await incScoped.CountAsync(i => i.Status == IncidentStatus.New || i.Status == IncidentStatus.InProgress);
        var totalProblems = await probScoped.CountAsync();

        ViewBag.TotalServices = totalServices;
        ViewBag.InternalServices = internalServices;
        ViewBag.ExternalServices = externalServices;
        ViewBag.TotalIncidents = totalIncidents;
        ViewBag.OpenIncidents = openIncidents;
        ViewBag.TotalProblems = totalProblems;

        // Get recent services (scoped)
        var recentServices = await svcScoped
            .Include(s => s.OwningUnit)
            .Include(s => s.StrategicObjective)
            .OrderByDescending(s => s.UpdatedAt)
            .Take(10)
            .ToListAsync();

        return View(recentServices);
    }

    /// <summary>
    /// List all services
    /// </summary>
    public async Task<IActionResult> Index(ServiceType? type = null, string? categoryId = null)
    {
        var query = _context.Services
            .Where(s => !s.IsDeleted)
            .Include(s => s.OwningUnit)
            .Include(s => s.StrategicObjective)
            .Include(s => s.ServiceCategory)
            .Include(s => s.Processes.Where(p => !p.IsDeleted))
            .AsQueryable();

        var scope = await _scopingService.GetScopeAsync(User);
        query = query.ApplyOwningUnitScope(scope);

        if (type.HasValue)
            query = query.Where(s => s.ServiceType == type.Value);
        if (!string.IsNullOrWhiteSpace(categoryId))
            query = query.Where(s => s.ServiceCategoryId == categoryId);

        var services = await query.OrderBy(s => s.DisplayOrder).ToListAsync();

        ViewBag.SelectedType = type;
        ViewBag.SelectedCategoryId = categoryId;
        ViewBag.CategoryFilterOptions = await _context.ServiceCategories
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Code)
            .ToListAsync();
        return View(services);
    }

    /// <summary>
    /// View service details
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var service = await _context.Services
            .Include(s => s.OwningUnit)
            .Include(s => s.ServiceCategory)
            .Include(s => s.StrategicObjective)
            // Strategic Objective is stored two ways on a Service: a single FK
            // (.StrategicObjective above) AND a many-to-many table for additional
            // objectives (.ServiceStrategicObjectives). The Edit form's multi-select
            // writes to the M:M only, so the Details page has to load both and the
            // view falls back from M:M to the single FK. Same dual-storage pattern
            // as Process.ProcessStrategicObjectives.
            .Include(s => s.ServiceStrategicObjectives)
                .ThenInclude(sso => sso.StrategicObjective)
            .Include(s => s.Processes.Where(p => !p.IsDeleted))
                .ThenInclude(p => p.ProcessGroup)
            .Include(s => s.Measurements.Where(m => m.IsActive))
            .Include(s => s.ServiceAssets)
                .ThenInclude(sa => sa.Asset)
                    .ThenInclude(a => a.Category)
            .Include(s => s.ServiceRisks)
                .ThenInclude(sr => sr.Risk)
                    .ThenInclude(r => r.Category)
            .Include(s => s.ProcessServices)
                .ThenInclude(ps => ps.Process)
                    .ThenInclude(p => p.ProcessGroup)
            .Include(s => s.CatalogInfo)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        if (service == null)
            return NotFound();

        // SEC-001: record-level scope (IDOR). Block direct-URL access to a
        // service outside the caller's org scope. 404 not 403 — don't leak existence.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(service))
            return NotFound();

        // F-CC-002: surface change requests linked to this service.
        ViewBag.RelatedChangeRequests = await _context.ChangeRequests
            .Where(cr => !cr.IsDeleted && cr.ServiceId == id)
            .OrderByDescending(cr => cr.CreatedAt)
            .Take(5)
            .ToListAsync();
        ViewBag.OpenChangeRequestCount = await _context.ChangeRequests
            .CountAsync(cr => !cr.IsDeleted && cr.ServiceId == id
                && cr.Status != ChangeRequestStatus.Implemented
                && cr.Status != ChangeRequestStatus.Rejected
                && cr.Status != ChangeRequestStatus.Cancelled);

        return View(service);
    }

    /// <summary>
    /// Create service form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Service.Create)]
    public async Task<IActionResult> Create(string? owningUnitId = null)
    {
        await PopulateDropdowns();
        int? owningUnitIdInt = int.TryParse(owningUnitId, out var ouid) ? ouid : (int?)null;
        if (owningUnitIdInt != null)
        {
            // Pre-select + lock the owning unit when arriving from /ProcessHierarchy
            var unit = await _context.OrganizationUnits
                .FirstOrDefaultAsync(o => o.Id == owningUnitIdInt);
            ViewBag.LockedOwningUnitId = owningUnitId;
            ViewBag.LockedOwningUnitName = unit == null ? null
                : (System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? unit.NameAr : unit.NameEn);
        }
        return View(new Service
        {
            OwningUnitId = owningUnitIdInt
        });
    }

    /// <summary>
    /// Create service
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Service.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Service service, List<string>? SelectedProcessIds,
        List<string>? SelectedAssetIds, List<string>? SelectedRiskIds,
        List<string>? SelectedObjectiveIds,
        string? TagList,
        // Optional "Advanced" catalog content from the collapsed wizard section.
        // Any non-empty value here creates a Draft ServiceCatalogInfo row.
        decimal? CatalogDurationValue, ESEMS.Web.Models.Enums.TimeUnit? CatalogDurationUnit,
        decimal? CatalogFeeAmount, bool CatalogIsFree, string? CatalogFeeNote,
        List<ESEMS.Web.Models.Enums.ServiceDeliveryChannel>? CatalogDeliveryChannels,
        string? CatalogTargetAudienceEn, string? CatalogTargetAudienceAr,
        string? CatalogPreConditionsEn, string? CatalogPreConditionsAr,
        string? CatalogPoliciesEn, string? CatalogPoliciesAr,
        string? CatalogProcedureEn, string? CatalogProcedureAr)
    {
        // Service Code is system-generated; strip any posted value + clear the
        // [Required] error. Final allocation happens inside the retry block
        // below to defend against concurrent inserts.
        service.Code = string.Empty;
        ModelState.Remove(nameof(service.Code));

        // DisplayOrder is hidden too: append to the end of the OwningUnit.
        if (service.DisplayOrder == 0)
        {
            service.DisplayOrder = await GetNextServiceDisplayOrderAsync(service.OwningUnitId);
        }

        if (ModelState.IsValid)
        {
            // Pre-allocate the code so the rest of the create flow has it.
            // The save itself happens lower in this method; if a concurrent
            // insert wins the race the unique-constraint violation triggers
            // a retry that reallocates.
            service.Code = await GenerateNextServiceCodeAsync();

            // Set tags from tag input
            if (!string.IsNullOrWhiteSpace(TagList))
            {
                service.Tags = TagList;
            }

            service.Id = Guid.NewGuid().ToString();
            service.CreatedAt = DateTime.UtcNow;
            service.UpdatedAt = DateTime.UtcNow;
            service.CreatedById = User.Identity?.Name;

            _context.Services.Add(service);

            // Add ProcessService relationships for selected processes
            if (SelectedProcessIds != null && SelectedProcessIds.Any())
            {
                foreach (var processId in SelectedProcessIds)
                {
                    _context.ProcessServices.Add(new ProcessService
                    {
                        ProcessId = processId,
                        ServiceId = service.Id,
                        Criticality = 3,
                        IsMandatory = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // Add ServiceAsset relationships for selected assets
            if (SelectedAssetIds != null && SelectedAssetIds.Any())
            {
                foreach (var assetId in SelectedAssetIds)
                {
                    _context.ServiceAssets.Add(new ServiceAsset
                    {
                        ServiceId = service.Id,
                        AssetId = assetId,
                        Criticality = 3,
                        IsRequired = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        UpdatedById = User.Identity?.Name
                    });
                }
            }

            // Add ServiceRisk relationships for selected risks
            if (SelectedRiskIds != null && SelectedRiskIds.Any())
            {
                foreach (var riskId in SelectedRiskIds)
                {
                    _context.ServiceRisks.Add(new ServiceRisk
                    {
                        ServiceId = service.Id,
                        RiskId = riskId,
                        ImpactLevel = 3,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        UpdatedById = User.Identity?.Name
                    });
                }
            }

            // Add ServiceStrategicObjective relationships for selected objectives
            if (SelectedObjectiveIds != null && SelectedObjectiveIds.Any())
            {
                foreach (var objectiveId in SelectedObjectiveIds)
                {
                    _context.ServiceStrategicObjectives.Add(new ServiceStrategicObjective
                    {
                        ServiceId = service.Id,
                        StrategicObjectiveId = objectiveId,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // Advanced (optional) catalog content — collapsed section of the
            // wizard. Only materialize a Draft CatalogInfo row when at least
            // one field was touched; otherwise leave the service without a
            // sidecar (user can draft later from the Catalog tab).
            string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            var hasStructured = CatalogDurationValue.HasValue || CatalogDurationUnit.HasValue
                || CatalogFeeAmount.HasValue || CatalogIsFree
                || !string.IsNullOrWhiteSpace(CatalogFeeNote)
                || (CatalogDeliveryChannels?.Any() ?? false);
            var hasNarrative = !string.IsNullOrWhiteSpace(CatalogTargetAudienceEn) || !string.IsNullOrWhiteSpace(CatalogTargetAudienceAr)
                || !string.IsNullOrWhiteSpace(CatalogPreConditionsEn) || !string.IsNullOrWhiteSpace(CatalogPreConditionsAr)
                || !string.IsNullOrWhiteSpace(CatalogPoliciesEn) || !string.IsNullOrWhiteSpace(CatalogPoliciesAr)
                || !string.IsNullOrWhiteSpace(CatalogProcedureEn) || !string.IsNullOrWhiteSpace(CatalogProcedureAr);
            if (hasStructured || hasNarrative)
            {
                var catalog = new ServiceCatalogInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceId = service.Id,
                    DurationValue    = CatalogDurationValue,
                    DurationUnit     = CatalogDurationUnit,
                    FeeAmount        = CatalogIsFree ? null : CatalogFeeAmount,
                    IsFree           = CatalogIsFree,
                    FeeNote          = Norm(CatalogFeeNote),
                    TargetAudienceEn = Norm(CatalogTargetAudienceEn),
                    TargetAudienceAr = Norm(CatalogTargetAudienceAr),
                    PreConditionsEn  = Norm(CatalogPreConditionsEn),
                    PreConditionsAr  = Norm(CatalogPreConditionsAr),
                    PoliciesEn       = Norm(CatalogPoliciesEn),
                    PoliciesAr       = Norm(CatalogPoliciesAr),
                    ProcedureEn      = Norm(CatalogProcedureEn),
                    ProcedureAr      = Norm(CatalogProcedureAr),
                    IsPublished = false, // Always Draft when created via the wizard
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedById = User.Identity?.Name,
                    Version = 1
                };
                if (CatalogDeliveryChannels?.Any() ?? false)
                    catalog.SetDeliveryChannelList(CatalogDeliveryChannels);
                _context.ServiceCatalogInfos.Add(catalog);
            }

            // Retry once on a unique-constraint race: another concurrent
            // insert grabbed our SVC- code first. Reallocate and try again.
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                var raceLost = await _context.Services
                    .AnyAsync(s => s.Code == service.Code && s.Id != service.Id);
                if (!raceLost) throw;
                service.Code = await GenerateNextServiceCodeAsync();
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = _localizer["Success_ServiceCreated"].Value;
            TempData["JustCreated"] = "1";
            return RedirectToAction(nameof(Details), new { id = service.Id });
        }

        await PopulateDropdowns();
        return View(service);
    }

    /// <summary>
    /// Edit service form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        // CatalogInfo eager-loaded so Step 3's Advanced collapsible can
        // pre-populate the 7 narrative textareas + IsPublished checkbox.
        var service = await _context.Services
            .Include(s => s.CatalogInfo)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (service == null) return NotFound();

        // SEC-001: record-level scope (IDOR) on the Edit form.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(service))
            return NotFound();

        // #14: only full-access (Unscoped/All) users may reassign the owning unit.
        ViewBag.CanReassignUnit = scope.IsUnscoped;

        await PopulateDropdowns();

        // Existing linked processes (M:M ProcessServices)
        ViewBag.ExistingProcessIds = await _context.ProcessServices
            .Where(ps => ps.ServiceId == id && ps.IsActive)
            .Select(ps => ps.ProcessId)
            .ToListAsync();

        // Existing required assets (M:M ServiceAssets)
        ViewBag.ExistingAssetIds = await _context.ServiceAssets
            .Where(sa => sa.ServiceId == id && sa.IsActive)
            .Select(sa => sa.AssetId)
            .ToListAsync();

        // Existing service risks (M:M ServiceRisks)
        ViewBag.ExistingRiskIds = await _context.ServiceRisks
            .Where(sr => sr.ServiceId == id && sr.IsActive)
            .Select(sr => sr.RiskId)
            .ToListAsync();

        // Existing strategic objectives (M:M ServiceStrategicObjectives)
        ViewBag.ExistingObjectiveIds = await _context.ServiceStrategicObjectives
            .Where(so => so.ServiceId == id && so.IsActive)
            .Select(so => so.StrategicObjectiveId)
            .ToListAsync();

        return View(service);
    }

    /// <summary>
    /// Edit service
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Service service,
        List<string>? SelectedProcessIds, List<string>? SelectedAssetIds,
        List<string>? SelectedRiskIds, List<string>? SelectedObjectiveIds,
        string? TagList,
        // Advanced — catalog content from Step 3's collapsible. Upserts the
        // 1:1 ServiceCatalogInfo sidecar; empty values trim to null. The
        // standalone EditCatalog page was retired in favor of this surface.
        bool CatalogIsPublished = false,
        decimal? CatalogDurationValue = null, ESEMS.Web.Models.Enums.TimeUnit? CatalogDurationUnit = null,
        decimal? CatalogFeeAmount = null, bool CatalogIsFree = false, string? CatalogFeeNote = null,
        List<ESEMS.Web.Models.Enums.ServiceDeliveryChannel>? CatalogDeliveryChannels = null,
        string? CatalogTargetAudienceEn = null, string? CatalogTargetAudienceAr = null,
        string? CatalogPreConditionsEn = null, string? CatalogPreConditionsAr = null,
        string? CatalogPoliciesEn = null, string? CatalogPoliciesAr = null,
        string? CatalogProcedureEn = null, string? CatalogProcedureAr = null)
    {
        if (id != service.Id)
            return NotFound();

        // SEC-001/SEC-002: load the persisted entity, gate on its real
        // OwningUnitId (IDOR), then load-then-patch only user-editable fields.
        // The bound `service` is untrusted — never _context.Update() it, or a
        // crafted post could overwrite OwningUnitId / IsDeleted / Code / audit
        // fields. Code/DisplayOrder are system-managed too.
        ModelState.Remove(nameof(Service.Code));
        ModelState.Remove(nameof(Service.DisplayOrder));

        var existing = await _context.Services
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (existing == null)
            return NotFound();

        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(existing))
            return NotFound();

        // #14: scoped users can't change the owning unit — drop it from
        // validation so a disabled (non-posted) dropdown doesn't fail ModelState.
        if (!scope.IsUnscoped)
            ModelState.Remove(nameof(Service.OwningUnitId));

        if (ModelState.IsValid)
        {
            // TagList from chip UI overrides the bound Tags input when posted.
            var newTags = TagList != null
                ? (string.IsNullOrWhiteSpace(TagList) ? null : TagList)
                : service.Tags;

            // Patch ONLY user-editable fields. Code/DisplayOrder stay whatever
            // the Create flow assigned; Id/CreatedAt/CreatedById/IsDeleted/
            // DeletedAt are never bound. StrategicObjectiveId is the deprecated
            // single-link shim — the M:M (SelectedObjectiveIds) is canonical,
            // so we leave the persisted value untouched.
            // #14: OwningUnitId is reassignable ONLY by full-access (Unscoped)
            // users; for scoped users it stays as persisted (no scope-escape).
            if (scope.IsUnscoped && service.OwningUnitId != null)
                existing.OwningUnitId = service.OwningUnitId;
            existing.NameEn                    = service.NameEn;
            existing.NameAr                    = service.NameAr;
            existing.DescriptionEn             = service.DescriptionEn;
            existing.DescriptionAr             = service.DescriptionAr;
            existing.ServiceType               = service.ServiceType;
            existing.Channel                   = service.Channel;
            existing.ServiceCategoryId         = service.ServiceCategoryId;
            existing.IsActive                  = service.IsActive;
            existing.Tags                      = newTags;
            existing.SLADays                   = service.SLADays;
            existing.TargetDeliveryDays        = service.TargetDeliveryDays;
            existing.ActualDeliveryDays        = service.ActualDeliveryDays;
            existing.ServiceFee                = service.ServiceFee;
            existing.CustomerSatisfactionScore = service.CustomerSatisfactionScore;
            existing.AnnualTransactionCount    = service.AnnualTransactionCount;
            existing.UpdatedAt                 = DateTime.UtcNow;
            existing.UpdatedById               = User.Identity?.Name;
            existing.Version++;

            // === Processes (M:M ProcessServices) — wipe & replace ===
            var existingPS = await _context.ProcessServices
                .Where(ps => ps.ServiceId == id)
                .ToListAsync();
            _context.ProcessServices.RemoveRange(existingPS);
            if (SelectedProcessIds != null && SelectedProcessIds.Any())
            {
                foreach (var processId in SelectedProcessIds)
                {
                    _context.ProcessServices.Add(new ProcessService
                    {
                        ProcessId = processId,
                        ServiceId = service.Id,
                        Criticality = 3,
                        IsMandatory = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // === Assets (M:M ServiceAssets) — wipe & replace ===
            var existingSA = await _context.ServiceAssets
                .Where(sa => sa.ServiceId == id)
                .ToListAsync();
            _context.ServiceAssets.RemoveRange(existingSA);
            if (SelectedAssetIds != null && SelectedAssetIds.Any())
            {
                foreach (var assetId in SelectedAssetIds)
                {
                    _context.ServiceAssets.Add(new ESEMS.Web.Models.Services.ServiceAsset
                    {
                        ServiceId = service.Id,
                        AssetId = assetId,
                        Criticality = 3,
                        IsRequired = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        UpdatedById = User.Identity?.Name
                    });
                }
            }

            // === Risks (M:M ServiceRisks) — wipe & replace ===
            var existingSR = await _context.ServiceRisks
                .Where(sr => sr.ServiceId == id)
                .ToListAsync();
            _context.ServiceRisks.RemoveRange(existingSR);
            if (SelectedRiskIds != null && SelectedRiskIds.Any())
            {
                foreach (var riskId in SelectedRiskIds)
                {
                    _context.ServiceRisks.Add(new ESEMS.Web.Models.Services.ServiceRisk
                    {
                        ServiceId = service.Id,
                        RiskId = riskId,
                        ImpactLevel = 3,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        UpdatedById = User.Identity?.Name
                    });
                }
            }

            // === Strategic Objectives (M:M ServiceStrategicObjectives) — wipe & replace ===
            var existingSSO = await _context.ServiceStrategicObjectives
                .Where(sso => sso.ServiceId == id)
                .ToListAsync();
            _context.ServiceStrategicObjectives.RemoveRange(existingSSO);
            if (SelectedObjectiveIds != null && SelectedObjectiveIds.Any())
            {
                foreach (var objectiveId in SelectedObjectiveIds)
                {
                    _context.ServiceStrategicObjectives.Add(new ServiceStrategicObjective
                    {
                        ServiceId = service.Id,
                        StrategicObjectiveId = objectiveId,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = User.Identity?.Name,
                        IsActive = true
                    });
                }
            }

            // === Catalog content (1:1 ServiceCatalogInfo) — upsert ===
            // First touch creates a Draft row. Going Published stamps audit.
            // Going back to Draft leaves the historical PublishedAt/By intact
            // (treat them as "last published" fields, not "currently published").
            string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            var hasStructured = CatalogDurationValue.HasValue || CatalogDurationUnit.HasValue
                || CatalogFeeAmount.HasValue || CatalogIsFree
                || !string.IsNullOrWhiteSpace(CatalogFeeNote)
                || (CatalogDeliveryChannels?.Any() ?? false);
            var catalogTouched = hasStructured ||
                !string.IsNullOrWhiteSpace(CatalogTargetAudienceEn) || !string.IsNullOrWhiteSpace(CatalogTargetAudienceAr) ||
                !string.IsNullOrWhiteSpace(CatalogPreConditionsEn) || !string.IsNullOrWhiteSpace(CatalogPreConditionsAr) ||
                !string.IsNullOrWhiteSpace(CatalogPoliciesEn) || !string.IsNullOrWhiteSpace(CatalogPoliciesAr) ||
                !string.IsNullOrWhiteSpace(CatalogProcedureEn) || !string.IsNullOrWhiteSpace(CatalogProcedureAr) ||
                CatalogIsPublished;

            var existingCatalog = await _context.ServiceCatalogInfos
                .FirstOrDefaultAsync(c => c.ServiceId == id);

            if (existingCatalog == null && catalogTouched)
            {
                var newCi = new ServiceCatalogInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceId = service.Id,
                    DurationValue    = CatalogDurationValue,
                    DurationUnit     = CatalogDurationUnit,
                    FeeAmount        = CatalogIsFree ? null : CatalogFeeAmount,
                    IsFree           = CatalogIsFree,
                    FeeNote          = Norm(CatalogFeeNote),
                    TargetAudienceEn = Norm(CatalogTargetAudienceEn),
                    TargetAudienceAr = Norm(CatalogTargetAudienceAr),
                    PreConditionsEn  = Norm(CatalogPreConditionsEn),
                    PreConditionsAr  = Norm(CatalogPreConditionsAr),
                    PoliciesEn       = Norm(CatalogPoliciesEn),
                    PoliciesAr       = Norm(CatalogPoliciesAr),
                    ProcedureEn      = Norm(CatalogProcedureEn),
                    ProcedureAr      = Norm(CatalogProcedureAr),
                    IsPublished = CatalogIsPublished,
                    PublishedAt = CatalogIsPublished ? DateTime.UtcNow : null,
                    PublishedById = CatalogIsPublished ? User.Identity?.Name : null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedById = User.Identity?.Name,
                    Version = 1
                };
                if (CatalogDeliveryChannels?.Any() ?? false)
                    newCi.SetDeliveryChannelList(CatalogDeliveryChannels);
                _context.ServiceCatalogInfos.Add(newCi);
            }
            else if (existingCatalog != null)
            {
                existingCatalog.DurationValue    = CatalogDurationValue;
                existingCatalog.DurationUnit     = CatalogDurationUnit;
                existingCatalog.FeeAmount        = CatalogIsFree ? null : CatalogFeeAmount;
                existingCatalog.IsFree           = CatalogIsFree;
                existingCatalog.FeeNote          = Norm(CatalogFeeNote);
                existingCatalog.SetDeliveryChannelList(CatalogDeliveryChannels ?? new List<ESEMS.Web.Models.Enums.ServiceDeliveryChannel>());
                existingCatalog.TargetAudienceEn = Norm(CatalogTargetAudienceEn);
                existingCatalog.TargetAudienceAr = Norm(CatalogTargetAudienceAr);
                existingCatalog.PreConditionsEn  = Norm(CatalogPreConditionsEn);
                existingCatalog.PreConditionsAr  = Norm(CatalogPreConditionsAr);
                existingCatalog.PoliciesEn       = Norm(CatalogPoliciesEn);
                existingCatalog.PoliciesAr       = Norm(CatalogPoliciesAr);
                existingCatalog.ProcedureEn      = Norm(CatalogProcedureEn);
                existingCatalog.ProcedureAr      = Norm(CatalogProcedureAr);
                var becamePublished = CatalogIsPublished && !existingCatalog.IsPublished;
                existingCatalog.IsPublished = CatalogIsPublished;
                if (becamePublished)
                {
                    existingCatalog.PublishedAt = DateTime.UtcNow;
                    existingCatalog.PublishedById = User.Identity?.Name;
                }
                existingCatalog.UpdatedAt = DateTime.UtcNow;
                existingCatalog.UpdatedById = User.Identity?.Name;
                existingCatalog.Version++;
            }

            try
            {
                await _context.SaveChangesAsync();

                TempData["Success"] = _localizer["Success_ServiceUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id = service.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                // DATA-009 — another user updated this service between our
                // load and our save. Detect deletion vs. concurrent edit,
                // surface a localized message so the user reloads and
                // re-applies their changes instead of silently last-write-
                // winning. Pairs with the same fix in ProcessesController.
                if (!await _context.Services.AnyAsync(s => s.Id == service.Id))
                    return NotFound();
                ModelState.AddModelError(string.Empty,
                    System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar")
                        ? "تم تعديل هذا السجل من قبل مستخدم آخر. يرجى تحديث الصفحة وإعادة المحاولة."
                        : "This record was modified by another user. Please reload the page and try again.");
            }
        }

        await PopulateDropdowns();

        // Reload existing links on validation failure
        ViewBag.ExistingProcessIds = await _context.ProcessServices
            .Where(ps => ps.ServiceId == id && ps.IsActive)
            .Select(ps => ps.ProcessId)
            .ToListAsync();
        ViewBag.ExistingAssetIds = await _context.ServiceAssets
            .Where(sa => sa.ServiceId == id && sa.IsActive)
            .Select(sa => sa.AssetId)
            .ToListAsync();
        ViewBag.ExistingRiskIds = await _context.ServiceRisks
            .Where(sr => sr.ServiceId == id && sr.IsActive)
            .Select(sr => sr.RiskId)
            .ToListAsync();
        ViewBag.ExistingObjectiveIds = await _context.ServiceStrategicObjectives
            .Where(so => so.ServiceId == id && so.IsActive)
            .Select(so => so.StrategicObjectiveId)
            .ToListAsync();

        return View(service);
    }

    /// <summary>
    /// Delete service (soft delete). Refuses when active dependents exist —
    /// processes, assets, risks, catalog sidecar, incidents, problems, or
    /// improvement links. Audit finding C8 (2026-05-19 QA): the previous
    /// version soft-deleted unconditionally, orphaning every M2M and FK.
    ///
    /// RBAC-001 (QA 2026-06-02): this action previously required CanAdmin
    /// (the *.* wildcard), so granting a role Service.Delete in the matrix had
    /// no effect — only full administrators could delete. Every other action
    /// on this controller, and the parallel Incident/Problem controllers, gate
    /// on the granular Module.<Entity>.Delete policy. Aligned here so the
    /// permission matrix is honored. Record-level scope + dependency guards
    /// below remain the real safety net.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Service.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var service = await _context.Services.FindAsync(id);
        if (service == null)
            return NotFound();

        // SEC-001: record-level scope (IDOR) on delete.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(service))
            return NotFound();

        var processCount = await _context.ProcessServices
            .CountAsync(ps => ps.ServiceId == id && ps.IsActive);
        var assetCount = await _context.ServiceAssets
            .CountAsync(sa => sa.ServiceId == id && sa.IsActive);
        var riskCount = await _context.ServiceRisks
            .CountAsync(sr => sr.ServiceId == id && sr.IsActive);
        var responsibilityCount = await _context.ServiceResponsibilities
            .CountAsync(sr => sr.ServiceId == id && sr.IsActive);
        var objectiveCount = await _context.ServiceStrategicObjectives
            .CountAsync(so => so.ServiceId == id && so.IsActive);
        var incidentCount = await _context.Incidents
            .CountAsync(i => i.ServiceId == id && !i.IsDeleted);
        var problemCount = await _context.Problems
            .CountAsync(p => p.ServiceId == id && !p.IsDeleted);
        var improvementLinkCount = await _context.ImprovementServices
            .CountAsync(im => im.ServiceId == id);

        var total = processCount + assetCount + riskCount + responsibilityCount
                  + objectiveCount + incidentCount + problemCount + improvementLinkCount;
        if (total > 0)
        {
            var parts = new List<string>();
            if (processCount > 0) parts.Add($"{processCount} process(es)");
            if (assetCount > 0) parts.Add($"{assetCount} asset(s)");
            if (riskCount > 0) parts.Add($"{riskCount} risk(s)");
            if (responsibilityCount > 0) parts.Add($"{responsibilityCount} responsibility link(s)");
            if (objectiveCount > 0) parts.Add($"{objectiveCount} strategic objective link(s)");
            if (incidentCount > 0) parts.Add($"{incidentCount} incident(s)");
            if (problemCount > 0) parts.Add($"{problemCount} problem(s)");
            if (improvementLinkCount > 0) parts.Add($"{improvementLinkCount} improvement link(s)");
            TempData["Error"] = $"Cannot delete this service — it has active dependencies: {string.Join(", ", parts)}. Reassign or remove them first.";
            return RedirectToAction(nameof(Details), new { id });
        }

        service.IsDeleted = true;
        service.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_ServiceDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Returns the next DisplayOrder to append a service to the end of its
    /// OwningUnit. Increments by 10 so manual reorderings still have gaps.
    /// </summary>
    private async Task<int> GetNextServiceDisplayOrderAsync(int? owningUnitId)
    {
        var query = _context.Services.AsQueryable();
        query = owningUnitId == null
            ? query.Where(s => s.OwningUnitId == null)
            : query.Where(s => s.OwningUnitId == owningUnitId);
        var maxOrder = await query
            .Select(s => (int?)s.DisplayOrder)
            .MaxAsync() ?? 0;
        return maxOrder + 10;
    }

    /// <summary>
    /// Generates the next sequential service code in the format `SVC-NNNN`
    /// based on the highest existing matching code (including soft-deleted
    /// rows so codes are never reused).
    /// </summary>
    private async Task<string> GenerateNextServiceCodeAsync()
    {
        const string prefix = "SVC-";
        var existingCodes = await _context.Services
            .Where(s => s.Code.StartsWith(prefix))
            .Select(s => s.Code)
            .ToListAsync();
        var maxNum = existingCodes
            .Select(c => int.TryParse(c.Substring(prefix.Length), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{(maxNum + 1):D4}";
    }

    // Catalog content is now edited inside Service Edit's Step 3 "Advanced"
    // collapsible (controller logic above). The standalone EditCatalog action
    // and its view were removed once the merged surface landed.

    private async Task PopulateDropdowns()
    {
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");

        ViewBag.OrganizationUnits = new SelectList(
            await _context.OrganizationUnits.Where(u => !u.IsDeleted && u.IsActive).ToListAsync(),
            "Id", "Name");
        ViewBag.StrategicObjectives = new SelectList(
            await _context.StrategicObjectives.Where(so => !so.IsDeleted).ToListAsync(),
            "Id", "Name");

        var categories = await _context.ServiceCategories
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Code)
            .ToListAsync();
        ViewBag.ServiceCategories = new SelectList(categories, "Id", "Name");

        var processes = await _context.Processes.Where(p => !p.IsDeleted).OrderBy(p => p.Code).ToListAsync();
        ViewBag.Processes = new SelectList(
            processes.Select(p => new { p.Id, DisplayName = $"{p.Code} - {(isArabic ? p.NameAr : p.NameEn)}" }),
            "Id", "DisplayName");

        var assets = await _context.Assets.Where(a => !a.IsDeleted).OrderBy(a => a.AssetTag).ToListAsync();
        ViewBag.Assets = new SelectList(
            assets.Select(a => new { a.Id, DisplayName = $"{a.AssetTag} - {(isArabic ? a.NameAr : a.NameEn)}" }),
            "Id", "DisplayName");

        var risks = await _context.EnterpriseRisks.Where(r => !r.IsDeleted).OrderBy(r => r.RiskNumber).ToListAsync();
        ViewBag.Risks = new SelectList(
            risks.Select(r => new { r.Id, DisplayName = $"{r.RiskNumber} - {(isArabic ? r.NameAr : r.NameEn)}" }),
            "Id", "DisplayName");
    }


    #region Service-Asset Relationship Management

    /// <summary>
    /// Link an asset to a service
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> LinkAsset(string serviceId, string assetId, int criticality = 3, bool isRequired = true, string? usageDescription = null)
    {
        try
        {
            // Validate service exists
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null || service.IsDeleted)
                return Json(new { success = false, message = _localizer["Error_ServiceNotFound"].Value });

            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new { success = false, message = _localizer["Error_ServiceNotFound"].Value });

            // Validate asset exists
            var asset = await _context.Assets.FindAsync(assetId);
            if (asset == null || asset.IsDeleted)
                return Json(new { success = false, message = _localizer["Error_AssetNotFound"].Value });

            // Check if relationship already exists
            var existing = await _context.Set<ServiceAsset>()
                .FirstOrDefaultAsync(sa => sa.ServiceId == serviceId && sa.AssetId == assetId);

            if (existing != null)
                return Json(new { success = false, message = "This asset is already linked to this service." });

            // Create relationship
            var serviceAsset = new ServiceAsset
            {
                ServiceId = serviceId,
                AssetId = assetId,
                Criticality = criticality,
                IsRequired = isRequired,
                UsageDescription = usageDescription,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedById = User.Identity?.Name,
                UpdatedById = User.Identity?.Name
            };

            _context.Set<ServiceAsset>().Add(serviceAsset);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Asset {AssetId} linked to Service {ServiceId} by {User}", assetId, serviceId, User.Identity?.Name);

            return Json(new { success = true, message = "Asset linked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking asset {AssetId} to service {ServiceId}", assetId, serviceId);
            return Json(new { success = false, message = "An error occurred while linking the asset." });
        }
    }

    /// <summary>
    /// Unlink an asset from a service
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> UnlinkAsset(string serviceId, string assetId)
    {
        try
        {
            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new { success = false, message = "Asset relationship not found." });

            var serviceAsset = await _context.Set<ServiceAsset>()
                .FirstOrDefaultAsync(sa => sa.ServiceId == serviceId && sa.AssetId == assetId);

            if (serviceAsset == null)
                return Json(new { success = false, message = "Asset relationship not found." });

            _context.Set<ServiceAsset>().Remove(serviceAsset);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Asset {AssetId} unlinked from Service {ServiceId} by {User}", assetId, serviceId, User.Identity?.Name);

            return Json(new { success = true, message = "Asset unlinked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking asset {AssetId} from service {ServiceId}", assetId, serviceId);
            return Json(new { success = false, message = "An error occurred while unlinking the asset." });
        }
    }

    /// <summary>
    /// Update service-asset relationship details
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> UpdateServiceAsset(string serviceId, string assetId, int criticality, bool isRequired, string? usageDescription = null)
    {
        try
        {
            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new { success = false, message = "Asset relationship not found." });

            var serviceAsset = await _context.Set<ServiceAsset>()
                .FirstOrDefaultAsync(sa => sa.ServiceId == serviceId && sa.AssetId == assetId);

            if (serviceAsset == null)
                return Json(new { success = false, message = "Asset relationship not found." });

            serviceAsset.Criticality = criticality;
            serviceAsset.IsRequired = isRequired;
            serviceAsset.UsageDescription = usageDescription;
            serviceAsset.UpdatedAt = DateTime.UtcNow;
            serviceAsset.UpdatedById = User.Identity?.Name;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Service-Asset relationship updated for Service {ServiceId} and Asset {AssetId} by {User}", serviceId, assetId, User.Identity?.Name);

            return Json(new { success = true, message = "Relationship updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service-asset relationship for Service {ServiceId} and Asset {AssetId}", serviceId, assetId);
            return Json(new { success = false, message = "An error occurred while updating the relationship." });
        }
    }

    /// <summary>
    /// Get available assets for linking (not already linked to this service)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableAssets(string serviceId)
    {
        try
        {
            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new List<object>());

            var linkedAssetIds = await _context.Set<ServiceAsset>()
                .Where(sa => sa.ServiceId == serviceId)
                .Select(sa => sa.AssetId)
                .ToListAsync();

            var availableAssets = await _context.Assets
                .Where(a => a.Status != AssetStatus.Disposed && !linkedAssetIds.Contains(a.Id))
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
            _logger.LogError(ex, "Error getting available assets for service {ServiceId}", serviceId);
            return Json(new List<object>());
        }
    }

    #endregion

    #region Service-Risk Relationship Management

    /// <summary>
    /// Link a risk to a service
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> LinkRisk(string serviceId, string riskId, int impactLevel = 3, string? specificControls = null, string? notes = null)
    {
        try
        {
            // Validate service exists
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null || service.IsDeleted)
                return Json(new { success = false, message = _localizer["Error_ServiceNotFound"].Value });

            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new { success = false, message = _localizer["Error_ServiceNotFound"].Value });

            // Validate risk exists
            var risk = await _context.EnterpriseRisks.FindAsync(riskId);
            if (risk == null || risk.IsDeleted)
                return Json(new { success = false, message = _localizer["Error_RiskNotFound"].Value });

            // Check if relationship already exists
            var existing = await _context.Set<ServiceRisk>()
                .FirstOrDefaultAsync(sr => sr.ServiceId == serviceId && sr.RiskId == riskId);

            if (existing != null)
                return Json(new { success = false, message = "This risk is already linked to this service." });

            // Create relationship
            var serviceRisk = new ServiceRisk
            {
                ServiceId = serviceId,
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

            _context.Set<ServiceRisk>().Add(serviceRisk);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Risk {RiskId} linked to Service {ServiceId} by {User}", riskId, serviceId, User.Identity?.Name);

            return Json(new { success = true, message = "Risk linked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking risk {RiskId} to service {ServiceId}", riskId, serviceId);
            return Json(new { success = false, message = "An error occurred while linking the risk." });
        }
    }

    /// <summary>
    /// Unlink a risk from a service
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> UnlinkRisk(string serviceId, string riskId)
    {
        try
        {
            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new { success = false, message = "Risk relationship not found." });

            var serviceRisk = await _context.Set<ServiceRisk>()
                .FirstOrDefaultAsync(sr => sr.ServiceId == serviceId && sr.RiskId == riskId);

            if (serviceRisk == null)
                return Json(new { success = false, message = "Risk relationship not found." });

            _context.Set<ServiceRisk>().Remove(serviceRisk);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Risk {RiskId} unlinked from Service {ServiceId} by {User}", riskId, serviceId, User.Identity?.Name);

            return Json(new { success = true, message = "Risk unlinked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking risk {RiskId} from service {ServiceId}", riskId, serviceId);
            return Json(new { success = false, message = "An error occurred while unlinking the risk." });
        }
    }

    /// <summary>
    /// Update service-risk relationship details
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> UpdateServiceRisk(string serviceId, string riskId, int impactLevel, string? specificControls = null, string? notes = null)
    {
        try
        {
            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new { success = false, message = "Risk relationship not found." });

            var serviceRisk = await _context.Set<ServiceRisk>()
                .FirstOrDefaultAsync(sr => sr.ServiceId == serviceId && sr.RiskId == riskId);

            if (serviceRisk == null)
                return Json(new { success = false, message = "Risk relationship not found." });

            serviceRisk.ImpactLevel = impactLevel;
            serviceRisk.SpecificControls = specificControls;
            serviceRisk.Notes = notes;
            serviceRisk.UpdatedAt = DateTime.UtcNow;
            serviceRisk.UpdatedById = User.Identity?.Name;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Service-Risk relationship updated for Service {ServiceId} and Risk {RiskId} by {User}", serviceId, riskId, User.Identity?.Name);

            return Json(new { success = true, message = "Relationship updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service-risk relationship for Service {ServiceId} and Risk {RiskId}", serviceId, riskId);
            return Json(new { success = false, message = "An error occurred while updating the relationship." });
        }
    }

    /// <summary>
    /// Get available risks for linking (not already linked to this service)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableRisks(string serviceId)
    {
        try
        {
            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new List<object>());

            var linkedRiskIds = await _context.Set<ServiceRisk>()
                .Where(sr => sr.ServiceId == serviceId)
                .Select(sr => sr.RiskId)
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
            _logger.LogError(ex, "Error getting available risks for service {ServiceId}", serviceId);
            return Json(new List<object>());
        }
    }

    #endregion

    #region Service-Process Linking API

    /// <summary>
    /// Link a process to this service
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> LinkProcess(string serviceId, string processId, int criticality = 3, bool isMandatory = true, string? notes = null)
    {
        try
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null || service.IsDeleted)
                return Json(new { success = false, message = _localizer["Error_ServiceNotFound"].Value });

            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new { success = false, message = _localizer["Error_ServiceNotFound"].Value });

            var process = await _context.Processes.FindAsync(processId);
            if (process == null || process.IsDeleted)
                return Json(new { success = false, message = "Process not found." });

            var existing = await _context.ProcessServices
                .FirstOrDefaultAsync(ps => ps.ProcessId == processId && ps.ServiceId == serviceId);

            if (existing != null)
                return Json(new { success = false, message = "This process is already linked to this service." });

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

            _logger.LogInformation("Process {ProcessId} linked to Service {ServiceId} by {User}", processId, serviceId, User.Identity?.Name);

            return Json(new { success = true, message = "Process linked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking process {ProcessId} to service {ServiceId}", processId, serviceId);
            return Json(new { success = false, message = "An error occurred while linking the process." });
        }
    }

    /// <summary>
    /// Unlink a process from this service
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> UnlinkProcess(string serviceId, string processId)
    {
        try
        {
            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new { success = false, message = "Process relationship not found." });

            var ps = await _context.ProcessServices
                .FirstOrDefaultAsync(ps => ps.ProcessId == processId && ps.ServiceId == serviceId);

            if (ps == null)
                return Json(new { success = false, message = "Process relationship not found." });

            _context.ProcessServices.Remove(ps);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Process {ProcessId} unlinked from Service {ServiceId} by {User}", processId, serviceId, User.Identity?.Name);

            return Json(new { success = true, message = "Process unlinked successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking process {ProcessId} from service {ServiceId}", processId, serviceId);
            return Json(new { success = false, message = "An error occurred while unlinking the process." });
        }
    }

    /// <summary>
    /// Update service-process relationship details
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> UpdateServiceProcess(string serviceId, string processId, int criticality, bool isMandatory, string? notes = null)
    {
        try
        {
            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
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

            _logger.LogInformation("Service-Process relationship updated for Service {ServiceId} and Process {ProcessId} by {User}", serviceId, processId, User.Identity?.Name);

            return Json(new { success = true, message = "Relationship updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service-process relationship for Service {ServiceId} and Process {ProcessId}", serviceId, processId);
            return Json(new { success = false, message = "An error occurred while updating the relationship." });
        }
    }

    /// <summary>
    /// Get available processes for linking (not already linked to this service)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableProcesses(string serviceId)
    {
        try
        {
            // SEC-009: record-level scope (IDOR) on the owning service.
            if (!await CanAccessServiceAsync(serviceId))
                return Json(new List<object>());

            var linkedProcessIds = await _context.ProcessServices
                .Where(ps => ps.ServiceId == serviceId)
                .Select(ps => ps.ProcessId)
                .ToListAsync();

            var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            var availableProcesses = await _context.Processes
                .Where(p => !p.IsDeleted && !linkedProcessIds.Contains(p.Id))
                .OrderBy(p => p.Code)
                .Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.NameEn,
                    p.NameAr,
                    Name = isArabic ? p.NameAr : p.NameEn
                })
                .ToListAsync();

            return Json(availableProcesses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available processes for service {ServiceId}", serviceId);
            return Json(new List<object>());
        }
    }

    #endregion

    /// <summary>
    /// SEC-009: record-level scope (IDOR) gate for the link/available helper
    /// actions, which only have a serviceId in hand. Resolves the owning
    /// service's OwningUnitId and checks it against the caller's scope.
    /// Returns true when the service is missing too — callers already handle
    /// the not-found case with their own message, and we don't want to leak
    /// existence via a different code path. Returns false only when the
    /// service exists but is out of scope.
    /// </summary>
    private async Task<bool> CanAccessServiceAsync(string serviceId)
    {
        var owner = await _context.Services.AsNoTracking()
            .Where(s => s.Id == serviceId && !s.IsDeleted)
            .Select(s => new { s.OwningUnitId })
            .FirstOrDefaultAsync();
        if (owner == null) return true; // missing — let the caller's own null check answer
        var scope = await _scopingService.GetScopeAsync(User);
        return scope.CanAccess(new ServiceScopeProbe(owner.OwningUnitId));
    }

    /// <summary>
    /// Lightweight IOwnedByUnit carrier for IDOR scope checks on helper actions
    /// that only have a serviceId/owning-unit projection in hand (no full
    /// Service entity). Mirrors AssetsController.AssignedScopeProbe.
    /// </summary>
    private sealed record ServiceScopeProbe(int? OwningUnitId)
        : ESEMS.Web.Models.Common.IOwnedByUnit;
}

