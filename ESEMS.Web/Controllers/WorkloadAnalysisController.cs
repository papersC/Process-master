using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.WorkloadAnalysis;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Security;
using ESEMS.Web.Extensions;
using ESEMS.Web.Services.Common;
using ESEMS.Web.Services.Workload;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ESEMS.Web.Controllers;

[Authorize(Policy = AppPolicies.Module.Workload.View)]
public class WorkloadAnalysisController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkloadAnalysisController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IScopingService _scopingService;

    public WorkloadAnalysisController(
        ApplicationDbContext context,
        ILogger<WorkloadAnalysisController> logger,
        IStringLocalizer<SharedResource> localizer,
        IScopingService scopingService)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _scopingService = scopingService;
    }

    // ─── Dashboard ───────────────────────────────────────────────────

    [Authorize(Policy = AppPolicies.Module.Workload.View)]
    public async Task<IActionResult> Dashboard()
    {
        var scope = await _scopingService.GetScopeAsync(User);

        var scenarios = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted)
            .ApplyOwningUnitScope(scope)
            .Include(s => s.Config)
            .Include(s => s.LineItems)
            .Include(s => s.OwningUnit)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        ViewBag.TotalScenarios = scenarios.Count;
        ViewBag.TotalFTERequired = scenarios.Where(s => s.Status == WorkloadScenarioStatus.Approved)
            .Sum(s => s.TotalFTE);
        ViewBag.TotalFTEGap = scenarios.Where(s => s.Status == WorkloadScenarioStatus.Approved)
            .Sum(s => s.FTEGap);
        // Not an average across scenarios — just the target rate from the
        // first available config (used as a headline KPI). Renamed from
        // "AvgUtilization" to avoid implying aggregation.
        ViewBag.DefaultUtilization = scenarios.FirstOrDefault()?.Config?.TargetUtilizationRate ?? 0.80m;

        // Data for charts
        var approvedScenarios = scenarios.Where(s => s.Status == WorkloadScenarioStatus.Approved).ToList();
        ViewBag.ChartLabels = approvedScenarios.Select(s => s.Name).ToList();
        ViewBag.ChartFTERequired = approvedScenarios.Select(s => Math.Round(s.TotalFTE, 1)).ToList();
        ViewBag.ChartHeadcount = approvedScenarios.Select(s => (decimal)(s.CurrentHeadcount ?? 0)).ToList();

        // Workload distribution by line item
        var allLineItems = approvedScenarios.SelectMany(s => s.LineItems).ToList();
        ViewBag.WorkloadLabels = allLineItems.Select(li => li.Name).Take(10).ToList();
        ViewBag.WorkloadHours = allLineItems.Select(li => Math.Round(li.WorkloadHours, 1)).Take(10).ToList();

        return View(scenarios.Take(10));
    }

    // ─── Index (list) ────────────────────────────────────────────────

    [Authorize(Policy = AppPolicies.Module.Workload.View)]
    public async Task<IActionResult> Index(WorkloadScenarioStatus? status, int? fiscalYear, string? unitId)
    {
        var scope = await _scopingService.GetScopeAsync(User);

        var query = _context.WorkloadScenarios
            .Where(s => !s.IsDeleted)
            .ApplyOwningUnitScope(scope)
            .Include(s => s.Config)
            .Include(s => s.LineItems)
            .Include(s => s.OwningUnit)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);
        if (fiscalYear.HasValue)
            query = query.Where(s => s.FiscalYear == fiscalYear.Value);
        if (!string.IsNullOrEmpty(unitId) && int.TryParse(unitId, out var unitIdInt))
            query = query.Where(s => s.OwningUnitId == unitIdInt);

        ViewBag.StatusFilter = status;
        ViewBag.YearFilter = fiscalYear;
        ViewBag.UnitFilter = unitId;
        await PopulateDropdowns();

        var scenarios = await query.OrderByDescending(s => s.UpdatedAt).ToListAsync();
        return View(scenarios);
    }

    // ─── Details ─────────────────────────────────────────────────────

    [Authorize(Policy = AppPolicies.Module.Workload.View)]
    public async Task<IActionResult> Details(string id)
    {
        var scenario = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted && s.Id == id)
            .Include(s => s.Config)
            .Include(s => s.LineItems).ThenInclude(li => li.Process)
            .Include(s => s.LineItems).ThenInclude(li => li.Service)
            .Include(s => s.OwningUnit)
            .FirstOrDefaultAsync();

        if (scenario == null) return NotFound();

        // F-003: record-level scope (IDOR). Block direct-URL access to a
        // scenario outside the caller's org scope. 404 not 403 — don't leak existence.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(scenario))
            return NotFound();

        // Audit trail — scan for logs on the scenario itself plus any of its
        // line items. The interceptor writes EntityId = the PK of whatever
        // row changed, so for line-item edits the match happens via the
        // lineItemIds set.
        var lineItemIds = scenario.LineItems.Select(li => li.Id).ToList();
        var auditLogs = await _context.AuditLogs
            .Where(a => (a.EntityType == "WorkloadScenario" && a.EntityId == id)
                     || (a.EntityType == "WorkloadLineItem" && lineItemIds.Contains(a.EntityId!)))
            .OrderByDescending(a => a.Timestamp)
            .Take(20)
            .ToListAsync();
        ViewBag.AuditLogs = auditLogs;

        return View(scenario);
    }

    // ─── Create ──────────────────────────────────────────────────────

    [Authorize(Policy = AppPolicies.Module.Workload.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        var model = new WorkloadScenario
        {
            FiscalYear = DateTime.UtcNow.Year,
            WorkloadConfigId = (await GetOrCreateDefaultConfig()).Id
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.Create)]
    public async Task<IActionResult> Create(WorkloadScenario model)
    {
        // Remove navigation properties from validation
        ModelState.Remove("Config");
        ModelState.Remove("OwningUnit");
        ModelState.Remove("LineItems");

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(model);
        }

        model.Id = Guid.NewGuid().ToString();
        model.Code = await GenerateScenarioCodeAsync();
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        model.CreatedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        _context.WorkloadScenarios.Add(model);
        await _context.SaveChangesAsync();

        TempData["Success"] = IsArabic
            ? $"تم إنشاء السيناريو {model.Code} بنجاح — أضف العمليات والخدمات أدناه"
            : $"Scenario {model.Code} created successfully — add processes and services below";

        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    // ─── Edit ────────────────────────────────────────────────────────

    [Authorize(Policy = AppPolicies.Module.Workload.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var scenario = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted && s.Id == id)
            .Include(s => s.Config)
            .Include(s => s.LineItems).ThenInclude(li => li.Process)
            .Include(s => s.LineItems).ThenInclude(li => li.Service)
            .Include(s => s.OwningUnit)
            .FirstOrDefaultAsync();

        if (scenario == null) return NotFound();

        // F-003: record-level scope (IDOR) on the Edit form.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(scenario))
            return NotFound();

        await PopulateDropdowns();

        // Status dropdown should only offer transitions the state machine
        // actually allows from the current state (plus the current state
        // itself as a no-op). Otherwise users pick an invalid target and
        // the POST bounces with a form-validation error.
        var allowed = new HashSet<WorkloadScenarioStatus>(
            WorkloadScenarioStatusMachine.AllowedNext(scenario.Status))
        { scenario.Status };
        ViewBag.AllowedStatuses = allowed;
        ViewBag.IsEditable = WorkloadScenarioStatusMachine.IsEditable(scenario.Status);

        return View(scenario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.Edit)]
    public async Task<IActionResult> Edit(string id, WorkloadScenario model)
    {
        if (id != model.Id) return NotFound();

        ModelState.Remove("Config");
        ModelState.Remove("OwningUnit");
        ModelState.Remove("LineItems");

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(model);
        }

        var existing = await _context.WorkloadScenarios.FindAsync(id);
        if (existing == null || existing.IsDeleted) return NotFound();

        // F-003: record-level scope (IDOR) on POST — gate on the PERSISTED
        // OwningUnitId, not whatever the form posted (mass-assignment defense).
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(existing))
            return NotFound();

        // Archived scenarios are terminal — don't allow form posts to mutate
        // them back to life.
        if (!WorkloadScenarioStatusMachine.IsEditable(existing.Status))
        {
            ModelState.AddModelError("Status",
                $"Scenario is {existing.Status} and cannot be edited.");
            await PopulateDropdowns();
            return View(model);
        }

        // Guard the status transition — users can't jump from Draft to
        // Archived-to-Approved-to-Draft by hand-crafting a form post.
        if (!WorkloadScenarioStatusMachine.CanTransition(existing.Status, model.Status))
        {
            var allowed = string.Join(", ",
                WorkloadScenarioStatusMachine.AllowedNext(existing.Status));
            ModelState.AddModelError("Status",
                $"Cannot move from '{existing.Status}' to '{model.Status}'. " +
                $"Allowed: {(string.IsNullOrEmpty(allowed) ? "(none)" : allowed)}.");
            await PopulateDropdowns();
            return View(model);
        }

        existing.NameEn = model.NameEn;
        existing.NameAr = model.NameAr;
        existing.DescriptionEn = model.DescriptionEn;
        existing.DescriptionAr = model.DescriptionAr;
        existing.FiscalYear = model.FiscalYear;
        existing.OwningUnitId = model.OwningUnitId;
        existing.WorkloadConfigId = model.WorkloadConfigId;
        existing.GrowthRatePercent = model.GrowthRatePercent;
        existing.ProjectionYears = model.ProjectionYears;
        existing.CurrentHeadcount = model.CurrentHeadcount;
        existing.Status = model.Status;
        existing.Notes = model.Notes;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    // ─── Delete ──────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.Delete)]
    public async Task<IActionResult> Delete(string id)
    {
        var scenario = await _context.WorkloadScenarios.FindAsync(id);
        if (scenario == null) return NotFound();

        // F-003: record-level scope (IDOR) on delete.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(scenario))
            return NotFound();

        scenario.IsDeleted = true;
        scenario.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // ─── Config ──────────────────────────────────────────────────────

    [Authorize(Policy = AppPolicies.Module.Workload.Edit)]
    public async Task<IActionResult> Config(string? unitId)
    {
        int? unitIdInt = int.TryParse(unitId, out var uid) ? uid : (int?)null;
        var config = await _context.WorkloadConfigs
            .Where(c => !c.IsDeleted && c.OrganizationUnitId == unitIdInt)
            .FirstOrDefaultAsync();

        config ??= await GetOrCreateDefaultConfig();

        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
        var units = await _context.OrganizationUnits
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.DisplayOrder)
            .ToListAsync();
        ViewBag.OrgUnits = units
            .Select(u => new SelectListItem(isArabic ? (u.NameAr ?? u.NameEn ?? "") : (u.NameEn ?? u.NameAr ?? ""), u.Id.ToString()))
            .ToList();

        ViewBag.SelectedUnitId = unitId;
        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.Edit)]
    public async Task<IActionResult> SaveConfig(WorkloadConfig model)
    {
        ModelState.Remove("OrganizationUnit");

        if (!ModelState.IsValid)
        {
            var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            var units = await _context.OrganizationUnits
                .Where(u => !u.IsDeleted).OrderBy(u => u.DisplayOrder).ToListAsync();
            ViewBag.OrgUnits = units
                .Select(u => new SelectListItem(isArabic ? (u.NameAr ?? u.NameEn ?? "") : (u.NameEn ?? u.NameAr ?? ""), u.Id.ToString()))
                .ToList();
            return View("Config", model);
        }

        var existing = await _context.WorkloadConfigs
            .Where(c => !c.IsDeleted && c.Id == model.Id)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.WorkingHoursPerDay = model.WorkingHoursPerDay;
            existing.WorkingDaysPerWeek = model.WorkingDaysPerWeek;
            existing.PublicHolidaysPerYear = model.PublicHolidaysPerYear;
            existing.AnnualLeaveDays = model.AnnualLeaveDays;
            existing.AverageSickDays = model.AverageSickDays;
            existing.TrainingDaysPerYear = model.TrainingDaysPerYear;
            existing.AdminOverheadPercent = model.AdminOverheadPercent;
            existing.TargetUtilizationRate = model.TargetUtilizationRate;
            existing.SupervisoryRatio = model.SupervisoryRatio;
            existing.FiscalYearStart = model.FiscalYearStart;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            model.Id = Guid.NewGuid().ToString();
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            if (string.IsNullOrEmpty(model.NameEn))
            {
                var unitName = model.OrganizationUnitId != null
                    ? (await _context.OrganizationUnits.FindAsync(model.OrganizationUnitId))?.NameEn ?? "Custom"
                    : "Global Default";
                model.NameEn = $"{unitName} Workload Config";
                model.NameAr = $"إعدادات حجم العمل - {unitName}";
            }
            _context.WorkloadConfigs.Add(model);
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = _localizer["Message_SavedSuccessfully"].Value;
        return RedirectToAction(nameof(Config), new { unitId = model.OrganizationUnitId });
    }

    // ─── AJAX: Line Items ────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.Edit)]
    public async Task<IActionResult> AddLineItem(string scenarioId, string? processId, string? serviceId)
    {
        // A line item without a process OR a service is a junk row — the
        // auto-populated name and volume would be empty. Require one.
        if (string.IsNullOrWhiteSpace(processId) && string.IsNullOrWhiteSpace(serviceId))
            return BadRequest(new { success = false, error = "Either processId or serviceId is required." });

        var scenario = await _context.WorkloadScenarios.FindAsync(scenarioId);
        if (scenario == null || scenario.IsDeleted) return NotFound();

        // F-003: record-level scope (IDOR) on the parent scenario.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(scenario))
            return NotFound();

        if (!WorkloadScenarioStatusMachine.IsEditable(scenario.Status))
            return BadRequest(new { success = false, error = $"Scenario is {scenario.Status} and cannot be modified." });

        var lineItem = new WorkloadLineItem
        {
            Id = Guid.NewGuid().ToString(),
            WorkloadScenarioId = scenarioId,
            ProcessId = processId,
            ServiceId = serviceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Auto-populate from linked entities
        if (!string.IsNullOrEmpty(processId))
        {
            var process = await _context.Processes.FindAsync(processId);
            if (process != null)
            {
                lineItem.NameEn = process.NameEn;
                lineItem.NameAr = process.NameAr;
                lineItem.AvgProcessingTimeMinutes = process.GetDurationInMinutes() ?? 0;
            }
        }
        if (!string.IsNullOrEmpty(serviceId))
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service != null)
            {
                if (string.IsNullOrEmpty(lineItem.NameEn))
                {
                    lineItem.NameEn = service.NameEn;
                    lineItem.NameAr = service.NameAr;
                }
                lineItem.AnnualVolume = service.AnnualTransactionCount ?? 0;
            }
        }

        _context.WorkloadLineItems.Add(lineItem);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            lineItem = new
            {
                lineItem.Id,
                lineItem.NameEn,
                lineItem.NameAr,
                lineItem.AnnualVolume,
                lineItem.AvgProcessingTimeMinutes,
                lineItem.WorkloadHours,
                lineItem.ComplexityEnabled
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.Edit)]
    public async Task<IActionResult> UpdateLineItem(string id, int annualVolume, decimal avgProcessingTimeMinutes,
        bool complexityEnabled, decimal? simplePercent, decimal? mediumPercent, decimal? complexPercent,
        decimal? simpleMult, decimal? mediumMult, decimal? complexMult, string? notes,
        string? seasonalDistribution)
    {
        // Baseline numeric sanity — FTE math is meaningless for negative
        // volumes or negative processing time.
        if (annualVolume < 0)
            return BadRequest(new { success = false, error = "annualVolume cannot be negative." });
        if (avgProcessingTimeMinutes < 0)
            return BadRequest(new { success = false, error = "avgProcessingTimeMinutes cannot be negative." });

        if (complexityEnabled)
        {
            // When complexity weighting is on, all three percentages must be
            // present and sum to 100 — otherwise the weighted volume silently
            // under- or over-counts. A 0.01 tolerance handles decimal rounding.
            var s = simplePercent  ?? 0m;
            var m = mediumPercent  ?? 0m;
            var c = complexPercent ?? 0m;
            if (s < 0 || m < 0 || c < 0 || s > 100 || m > 100 || c > 100)
                return BadRequest(new { success = false, error = "Complexity percentages must each be between 0 and 100." });
            if (Math.Abs(s + m + c - 100m) > 0.01m)
                return BadRequest(new { success = false, error = $"Complexity percentages must sum to 100 (got {s + m + c})." });

            if ((simpleMult ?? 0) < 0 || (mediumMult ?? 0) < 0 || (complexMult ?? 0) < 0)
                return BadRequest(new { success = false, error = "Complexity multipliers cannot be negative." });
        }

        var item = await _context.WorkloadLineItems.FindAsync(id);
        if (item == null) return NotFound();

        // Block edits on terminal scenarios so a stale page can't mutate
        // a scenario after it's been archived.
        var parent = await _context.WorkloadScenarios.FindAsync(item.WorkloadScenarioId);
        // F-003: record-level scope (IDOR) via the line item's parent scenario.
        var scope = await _scopingService.GetScopeAsync(User);
        if (parent != null && !scope.CanAccess(parent))
            return NotFound();
        if (parent != null && !WorkloadScenarioStatusMachine.IsEditable(parent.Status))
            return BadRequest(new { success = false, error = $"Scenario is {parent.Status} and cannot be modified." });

        // Seasonal distribution is an optional JSON array of 12 decimals
        // (monthly volume %). Validate shape + sum here so garbage doesn't
        // sneak into the database. Empty string / null clears any prior
        // distribution.
        if (!string.IsNullOrWhiteSpace(seasonalDistribution))
        {
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<decimal[]>(seasonalDistribution);
                if (arr == null || arr.Length != 12)
                    return BadRequest(new { success = false, error = "seasonalDistribution must be a JSON array of 12 numbers." });
                if (arr.Any(v => v < 0 || v > 100))
                    return BadRequest(new { success = false, error = "Each monthly percentage must be between 0 and 100." });
                if (Math.Abs(arr.Sum() - 100m) > 0.01m)
                    return BadRequest(new { success = false, error = $"Monthly percentages must sum to 100 (got {arr.Sum()})." });
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest(new { success = false, error = "seasonalDistribution must be valid JSON." });
            }
        }

        item.AnnualVolume = annualVolume;
        item.AvgProcessingTimeMinutes = avgProcessingTimeMinutes;
        item.ComplexityEnabled = complexityEnabled;
        item.SimpleVolumePercent = simplePercent;
        item.MediumVolumePercent = mediumPercent;
        item.ComplexVolumePercent = complexPercent;
        item.SimpleMult = simpleMult;
        item.MediumMult = mediumMult;
        item.ComplexMult = complexMult;
        item.Notes = notes;
        item.SeasonalDistribution = string.IsNullOrWhiteSpace(seasonalDistribution) ? null : seasonalDistribution;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new { success = true, workloadHours = Math.Round(item.WorkloadHours, 2) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.Edit)]
    public async Task<IActionResult> RemoveLineItem(string id)
    {
        var item = await _context.WorkloadLineItems.FindAsync(id);
        if (item == null) return NotFound();

        var parent = await _context.WorkloadScenarios.FindAsync(item.WorkloadScenarioId);
        // F-003: record-level scope (IDOR) via the line item's parent scenario.
        var scope = await _scopingService.GetScopeAsync(User);
        if (parent != null && !scope.CanAccess(parent))
            return NotFound();
        if (parent != null && !WorkloadScenarioStatusMachine.IsEditable(parent.Status))
            return BadRequest(new { success = false, error = $"Scenario is {parent.Status} and cannot be modified." });

        _context.WorkloadLineItems.Remove(item);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    // ─── AJAX: Auto-fill data ────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = AppPolicies.Module.Workload.View)]
    public async Task<IActionResult> GetProcessData(string processId)
    {
        var process = await _context.Processes
            .Where(p => !p.IsDeleted && p.Id == processId)
            .FirstOrDefaultAsync();

        if (process == null) return NotFound();

        // FU-001: record-level scope (IDOR). This helper feeds the line-item
        // builder; a scoped user must not read another unit's process data by id.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(process)) return NotFound();

        return Json(new
        {
            nameEn = process.NameEn,
            nameAr = process.NameAr,
            durationMinutes = process.GetDurationInMinutes() ?? 0
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.Module.Workload.View)]
    public async Task<IActionResult> GetServiceData(string serviceId)
    {
        var service = await _context.Services
            .Where(s => !s.IsDeleted && s.Id == serviceId)
            .FirstOrDefaultAsync();

        if (service == null) return NotFound();

        // FU-001: record-level scope (IDOR) — same as GetProcessData above.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(service)) return NotFound();

        return Json(new
        {
            nameEn = service.NameEn,
            nameAr = service.NameAr,
            annualVolume = service.AnnualTransactionCount ?? 0
        });
    }

    // ─── AJAX: Calculate ─────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.View)]
    public async Task<IActionResult> Calculate(string scenarioId)
    {
        var scenario = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted && s.Id == scenarioId)
            .Include(s => s.Config)
            .Include(s => s.LineItems)
            .FirstOrDefaultAsync();

        if (scenario == null) return NotFound();

        // F-003: record-level scope (IDOR) on the scenario.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(scenario))
            return NotFound();

        return Json(new
        {
            totalWorkloadHours = Math.Round(scenario.TotalWorkloadHours, 2),
            totalRequiredFTE = Math.Round(scenario.TotalRequiredFTE, 2),
            adjustedFTE = Math.Round(scenario.AdjustedFTE, 2),
            supervisoryFTE = Math.Round(scenario.SupervisoryFTE, 2),
            totalFTE = scenario.TotalFTE,
            fteGap = scenario.FTEGap,
            netAvailableHours = scenario.Config != null ? Math.Round(scenario.Config.NetAvailableHoursPerFTE, 2) : 0,
            lineItems = scenario.LineItems.Select(li => new
            {
                li.Id,
                workloadHours = Math.Round(li.WorkloadHours, 2),
                requiredFTE = Math.Round(li.RequiredFTE, 2)
            })
        });
    }

    // ─── Clone ───────────────────────────────────────────────────────

    /// <summary>
    /// Duplicates an existing scenario — header fields + every line item and
    /// its complexity config — so users can roll a previous year's analysis
    /// forward without re-entering everything. The clone starts in Draft
    /// with a fresh code (WS-NNN) and the target fiscal year. The scenario
    /// itself can be in any state; we don't gate cloning on IsEditable
    /// because we're creating a *new* record, not mutating the source.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.Create)]
    public async Task<IActionResult> Clone(string id, int? newFiscalYear)
    {
        var source = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted && s.Id == id)
            .Include(s => s.LineItems)
            .FirstOrDefaultAsync();

        if (source == null) return NotFound();

        // F-003: record-level scope (IDOR) on the source scenario.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(source))
            return NotFound();

        var targetYear = newFiscalYear ?? (source.FiscalYear + 1);

        var clone = new WorkloadScenario
        {
            Id = Guid.NewGuid().ToString(),
            Code = await GenerateScenarioCodeAsync(),
            NameEn = $"{source.NameEn} (Copy FY{targetYear})",
            NameAr = $"{source.NameAr} (نسخة {targetYear})",
            DescriptionEn = source.DescriptionEn,
            DescriptionAr = source.DescriptionAr,
            FiscalYear = targetYear,
            Status = WorkloadScenarioStatus.Draft,
            OwningUnitId = source.OwningUnitId,
            WorkloadConfigId = source.WorkloadConfigId,
            GrowthRatePercent = source.GrowthRatePercent,
            ProjectionYears = source.ProjectionYears,
            CurrentHeadcount = source.CurrentHeadcount,
            Notes = source.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        };
        _context.WorkloadScenarios.Add(clone);

        foreach (var li in source.LineItems)
        {
            _context.WorkloadLineItems.Add(new WorkloadLineItem
            {
                Id = Guid.NewGuid().ToString(),
                WorkloadScenarioId = clone.Id,
                ProcessId = li.ProcessId,
                ServiceId = li.ServiceId,
                NameEn = li.NameEn,
                NameAr = li.NameAr,
                AnnualVolume = li.AnnualVolume,
                AvgProcessingTimeMinutes = li.AvgProcessingTimeMinutes,
                ComplexityEnabled = li.ComplexityEnabled,
                SimpleVolumePercent = li.SimpleVolumePercent,
                MediumVolumePercent = li.MediumVolumePercent,
                ComplexVolumePercent = li.ComplexVolumePercent,
                SimpleMult = li.SimpleMult,
                MediumMult = li.MediumMult,
                ComplexMult = li.ComplexMult,
                SeasonalDistribution = li.SeasonalDistribution,
                Notes = li.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = IsArabic
            ? $"تم نسخ السيناريو إلى {clone.Code} للسنة {targetYear}"
            : $"Scenario cloned to {clone.Code} for FY{targetYear}";

        return RedirectToAction(nameof(Edit), new { id = clone.Id });
    }

    // ─── Export ──────────────────────────────────────────────────────

    /// <summary>
    /// Downloads a single scenario as CSV — scenario header row followed by
    /// one row per line item with the fields the user can see in the UI.
    /// UTF-8 BOM is included so Excel reads Arabic text correctly.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.Module.Workload.View)]
    public async Task<IActionResult> ExportScenario(string id)
    {
        var scenario = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted && s.Id == id)
            .Include(s => s.Config)
            .Include(s => s.LineItems).ThenInclude(li => li.Process)
            .Include(s => s.LineItems).ThenInclude(li => li.Service)
            .Include(s => s.OwningUnit)
            .FirstOrDefaultAsync();

        if (scenario == null) return NotFound();

        // F-003: record-level scope (IDOR) on CSV export.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(scenario))
            return NotFound();

        static string Esc(string? s) =>
            s == null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Scenario,Code,FiscalYear,Status,OwningUnit,CurrentHeadcount,GrowthRate%,ProjectionYears,TotalWorkloadHours,TotalRequiredFTE,TotalFTE,FTEGap");
        csv.AppendLine(string.Join(",",
            Esc(scenario.Name), Esc(scenario.Code), scenario.FiscalYear, scenario.Status,
            Esc(scenario.OwningUnit?.Name),
            scenario.CurrentHeadcount?.ToString() ?? "",
            scenario.GrowthRatePercent?.ToString() ?? "",
            scenario.ProjectionYears,
            Math.Round(scenario.TotalWorkloadHours, 2),
            Math.Round(scenario.TotalRequiredFTE, 2),
            scenario.TotalFTE,
            scenario.FTEGap));
        csv.AppendLine();
        csv.AppendLine("LineItem,AnnualVolume,AvgMinutes,ComplexityEnabled,Simple%,Medium%,Complex%,SimpleMult,MediumMult,ComplexMult,WorkloadHours");
        foreach (var li in scenario.LineItems.OrderBy(x => x.CreatedAt))
        {
            csv.AppendLine(string.Join(",",
                Esc(li.Name),
                li.AnnualVolume,
                li.AvgProcessingTimeMinutes,
                li.ComplexityEnabled,
                li.SimpleVolumePercent?.ToString() ?? "",
                li.MediumVolumePercent?.ToString() ?? "",
                li.ComplexVolumePercent?.ToString() ?? "",
                li.SimpleMult?.ToString() ?? "",
                li.MediumMult?.ToString() ?? "",
                li.ComplexMult?.ToString() ?? "",
                Math.Round(li.WorkloadHours, 2)));
        }

        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var bytes = bom.Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var fileName = $"{scenario.Code}_{DateTime.UtcNow:yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// Renders a one-page PDF summary of a scenario — header KPIs, the FTE
    /// breakdown, and every line item. QuestPDF Community license is set
    /// once at startup (Program.cs) so this just builds the document.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.Module.Workload.View)]
    public async Task<IActionResult> ExportScenarioPdf(string id)
    {
        var scenario = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted && s.Id == id)
            .Include(s => s.Config)
            .Include(s => s.LineItems).ThenInclude(li => li.Process)
            .Include(s => s.LineItems).ThenInclude(li => li.Service)
            .Include(s => s.OwningUnit)
            .FirstOrDefaultAsync();

        if (scenario == null) return NotFound();

        // F-003: record-level scope (IDOR) on PDF export.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(scenario))
            return NotFound();

        QuestPDF.Settings.License = LicenseType.Community;

        var brand = "#005B99";
        var header = $"{scenario.Code} — {scenario.Name}";
        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(ts => ts.FontSize(10).FontFamily(ESEMS.Web.Services.Export.PdfFonts.Family));

                page.Header().Column(col =>
                {
                    col.Item().Text("Mohammed Bin Rashid Housing Establishment").FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().Text(header).FontSize(18).Bold().FontColor(brand);
                    col.Item().Text($"Workload Analysis Scenario · FY{scenario.FiscalYear} · {scenario.Status}")
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    // KPI row
                    col.Item().Row(row =>
                    {
                        void Kpi(string label, string value, string? color = null)
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                            {
                                c.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().Text(value).FontSize(16).Bold().FontColor(color ?? Colors.Grey.Darken3);
                            });
                        }
                        Kpi("Total Workload Hours", scenario.TotalWorkloadHours.ToString("N0"));
                        row.ConstantItem(8);
                        Kpi("Total FTE", scenario.TotalFTE.ToString("N0"), brand);
                        row.ConstantItem(8);
                        Kpi("Current Headcount", scenario.CurrentHeadcount?.ToString("N0") ?? "—");
                        row.ConstantItem(8);
                        Kpi(scenario.FTEGap > 0 ? "Shortage" : "Surplus",
                            (scenario.FTEGap > 0 ? "+" : "") + scenario.FTEGap.ToString("N0"),
                            scenario.FTEGap > 0 ? Colors.Red.Darken1 : Colors.Green.Darken1);
                    });

                    // Parameters
                    col.Item().Text("Parameters").SemiBold().FontSize(12).FontColor(brand);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        void P(string k, string v) {
                            t.Cell().Padding(3).Text(k).FontColor(Colors.Grey.Medium).FontSize(9);
                            t.Cell().Padding(3).Text(v).SemiBold();
                        }
                        P("Config", scenario.Config?.NameEn ?? "—");
                        P("Net Hours / FTE", scenario.Config?.NetAvailableHoursPerFTE.ToString("N0") ?? "—");
                        P("Growth Rate", (scenario.GrowthRatePercent?.ToString("N1") ?? "0") + "% / yr");
                        P("Projection Years", scenario.ProjectionYears.ToString());
                        P("Base FTE", scenario.TotalRequiredFTE.ToString("N2"));
                        P("Adjusted FTE", scenario.AdjustedFTE.ToString("N2"));
                        P("Supervisory FTE", scenario.SupervisoryFTE.ToString("N2"));
                        P("Owning Unit", scenario.OwningUnit?.Name ?? "—");
                    });

                    // Line items
                    col.Item().Text($"Line Items ({scenario.LineItems.Count})").SemiBold().FontSize(12).FontColor(brand);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => {
                            c.RelativeColumn(3);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });
                        t.Header(h => {
                            foreach (var hh in new[] { "Name", "Annual Volume", "Avg Minutes", "Weighting", "Workload Hrs", "Required FTE" })
                                h.Cell().Background(brand).Padding(5).Text(hh).FontColor(Colors.White).SemiBold().FontSize(9);
                        });
                        foreach (var li in scenario.LineItems.OrderBy(x => x.CreatedAt))
                        {
                            var weight = li.ComplexityEnabled
                                ? $"{li.SimpleVolumePercent:0}/{li.MediumVolumePercent:0}/{li.ComplexVolumePercent:0}"
                                : "Flat";
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(li.Name).FontSize(9);
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(li.AnnualVolume.ToString("N0")).FontSize(9);
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(li.AvgProcessingTimeMinutes.ToString("N1")).FontSize(9);
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(weight).FontSize(9);
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(li.WorkloadHours.ToString("N1")).FontSize(9);
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(li.RequiredFTE.ToString("N2")).FontSize(9).FontColor(brand);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'")).FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span("  ·  Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();

        return File(pdf, "application/pdf", $"{scenario.Code}_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    // ─── Compare ─────────────────────────────────────────────────────

    /// <summary>
    /// Side-by-side comparison of two scenarios — parameters, totals, and
    /// union of line items. The "right" side can be omitted so the user
    /// first lands on the page with just the left scenario and a picker.
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Workload.View)]
    public async Task<IActionResult> Compare(string left, string? right)
    {
        var leftScenario = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted && s.Id == left)
            .Include(s => s.Config)
            .Include(s => s.LineItems)
            .Include(s => s.OwningUnit)
            .FirstOrDefaultAsync();

        if (leftScenario == null) return NotFound();

        // F-003: record-level scope (IDOR) on the left scenario.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(leftScenario))
            return NotFound();

        WorkloadScenario? rightScenario = null;
        if (!string.IsNullOrEmpty(right))
        {
            rightScenario = await _context.WorkloadScenarios
                .Where(s => !s.IsDeleted && s.Id == right)
                .Include(s => s.Config)
                .Include(s => s.LineItems)
                .Include(s => s.OwningUnit)
                .FirstOrDefaultAsync();

            // F-003: record-level scope (IDOR) on the right scenario too —
            // a scoped user must not compare against an out-of-scope scenario.
            if (rightScenario != null && !scope.CanAccess(rightScenario))
                return NotFound();
        }

        // Picker list — any non-deleted scenario except the left one.
        var others = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted && s.Id != left)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new SelectListItem(s.Code + " — " + s.NameEn, s.Id, s.Id == right))
            .ToListAsync();

        ViewBag.LeftScenario = leftScenario;
        ViewBag.RightScenario = rightScenario;
        ViewBag.OtherScenarios = others;
        return View();
    }

    // ─── Bulk re-home to a different Config ──────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Workload.Edit)]
    public async Task<IActionResult> BulkReassignConfig(string[] scenarioIds, string targetConfigId)
    {
        if (scenarioIds == null || scenarioIds.Length == 0 || string.IsNullOrEmpty(targetConfigId))
        {
            TempData["Error"] = _localizer["Error_SelectScenarioAndConfig"].Value;
            return RedirectToAction(nameof(Index));
        }

        var configExists = await _context.WorkloadConfigs.AnyAsync(c => !c.IsDeleted && c.Id == targetConfigId);
        if (!configExists) return NotFound();

        var scenarios = await _context.WorkloadScenarios
            .Where(s => !s.IsDeleted && scenarioIds.Contains(s.Id))
            .ToListAsync();

        // F-003: record-level scope (IDOR) — a scoped user must not bulk-reassign
        // scenarios outside their org subtree. Silently skip out-of-scope rows
        // (same treatment as terminal ones below) rather than failing the batch.
        var scope = await _scopingService.GetScopeAsync(User);

        int changed = 0;
        foreach (var s in scenarios)
        {
            if (!scope.CanAccess(s)) continue;
            // Don't mutate terminal scenarios — Archived is read-only.
            if (!WorkloadScenarioStatusMachine.IsEditable(s.Status)) continue;
            if (s.WorkloadConfigId == targetConfigId) continue;
            s.WorkloadConfigId = targetConfigId;
            s.UpdatedAt = DateTime.UtcNow;
            s.UpdatedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            changed++;
        }
        await _context.SaveChangesAsync();

        TempData["Success"] = IsArabic
            ? $"تم تغيير الإعدادات لـ {changed} سيناريو"
            : $"Config updated on {changed} scenario(s)";

        return RedirectToAction(nameof(Index));
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private async Task<string> GenerateScenarioCodeAsync()
    {
        // Scan ALL scenarios — soft-deleted included. The unique index on
        // Code does not filter on IsDeleted, so a deleted WS-006 still owns
        // that slot. Excluding deleted rows here used to cause duplicate-key
        // inserts the first time a user re-created after a delete.
        var allCodes = await _context.WorkloadScenarios
            .Where(s => s.Code.StartsWith("WS-"))
            .Select(s => s.Code)
            .ToListAsync();

        int max = 0;
        foreach (var code in allCodes)
        {
            if (int.TryParse(code[3..], out var num) && num > max)
                max = num;
        }

        return $"WS-{max + 1:D3}";
    }

    private async Task<WorkloadConfig> GetOrCreateDefaultConfig()
    {
        var config = await _context.WorkloadConfigs
            .Where(c => !c.IsDeleted && c.OrganizationUnitId == null)
            .FirstOrDefaultAsync();

        if (config == null)
        {
            config = new WorkloadConfig
            {
                Id = Guid.NewGuid().ToString(),
                NameEn = "UAE Government Default",
                NameAr = "الإعدادات الافتراضية للحكومة الإماراتية",
                DescriptionEn = "Default workload configuration with UAE government parameters",
                DescriptionAr = "إعدادات حجم العمل الافتراضية بمعايير الحكومة الإماراتية",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.WorkloadConfigs.Add(config);
            await _context.SaveChangesAsync();
        }

        return config;
    }

    private async Task PopulateDropdowns()
    {
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");

        var units = await _context.OrganizationUnits
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.DisplayOrder)
            .ToListAsync();
        ViewBag.OrgUnits = units
            .Select(u => new SelectListItem(isArabic ? (u.NameAr ?? u.NameEn ?? "") : (u.NameEn ?? u.NameAr ?? ""), u.Id.ToString()))
            .ToList();

        var configs = await _context.WorkloadConfigs
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.NameEn)
            .ToListAsync();
        ViewBag.Configs = configs
            .Select(c => new SelectListItem(isArabic ? (c.NameAr ?? c.NameEn ?? "") : (c.NameEn ?? c.NameAr ?? ""), c.Id))
            .ToList();

        var processes = await _context.Processes
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Code)
            .ToListAsync();
        ViewBag.Processes = processes
            .Select(p => new SelectListItem(p.Code + " - " + (isArabic ? (p.NameAr ?? p.NameEn ?? "") : (p.NameEn ?? p.NameAr ?? "")), p.Id))
            .ToList();

        var services = await _context.Services
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Code)
            .ToListAsync();
        ViewBag.Services = services
            .Select(s => new SelectListItem(s.Code + " - " + (isArabic ? (s.NameAr ?? s.NameEn ?? "") : (s.NameEn ?? s.NameAr ?? "")), s.Id))
            .ToList();

        ViewBag.Years = Enumerable.Range(DateTime.UtcNow.Year - 2, 5)
            .Select(y => new SelectListItem(y.ToString(), y.ToString()))
            .ToList();
    }
}
