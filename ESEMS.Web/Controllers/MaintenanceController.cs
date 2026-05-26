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
/// Controller for Maintenance Management (ISO 55001:2014)
/// Manages maintenance schedules and records for assets
/// </summary>
[Authorize(Policy = AppPolicies.Module.Asset.View)]
public class MaintenanceController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MaintenanceController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IScopingService _scopingService;

    public MaintenanceController(ApplicationDbContext context, ILogger<MaintenanceController> logger, IStringLocalizer<SharedResource> localizer, IScopingService scopingService)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _scopingService = scopingService;
    }

    // FUNC-008: maintenance schedules/records have no org-unit FK of their own —
    // they inherit scope from the Asset they hang off (Asset.AssignedToUnitId).
    // This probe lets us reuse ScopeContext.CanAccess(IAssignedToUnit) for the
    // per-record IDOR guard, exactly as AssetsController does for assets.
    private sealed record AssetScopeProbe(int? AssignedToUnitId)
        : ESEMS.Web.Models.Common.IAssignedToUnit;

    private async Task<bool> CanAccessAssetAsync(string? assetId)
    {
        var scope = await _scopingService.GetScopeAsync(User);
        if (scope.IsUnscoped) return true;
        if (string.IsNullOrEmpty(assetId)) return true;
        var assignedUnitId = await _context.Assets
            .Where(a => a.Id == assetId)
            .Select(a => a.AssignedToUnitId)
            .FirstOrDefaultAsync();
        return scope.CanAccess(new AssetScopeProbe(assignedUnitId));
    }

    // Bare /Maintenance → redirect to the schedules list. The sidebar only
    // links to /Maintenance/Schedules and /Maintenance/Records, but a stale
    // bookmark or typo on /Maintenance used to 404.
    public IActionResult Index() => RedirectToAction(nameof(Schedules));

    /// <summary>
    /// List all maintenance schedules
    /// </summary>
    public async Task<IActionResult> Schedules()
    {
        var query = _context.MaintenanceSchedules
            .Where(s => !s.IsDeleted)
            .Include(s => s.Asset)
                .ThenInclude(a => a.Category)
            .AsQueryable();

        // FUNC-008: scope by the linked Asset's AssignedToUnitId so a unit-scoped
        // user can't see another unit's maintenance. Schedules with no asset, or
        // an asset with no unit, stay visible (orphan-visible, same rule as the
        // QueryableScopeExtensions overloads).
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.IsUnscoped && scope.VisibleUnitIds != null)
        {
            query = query.Where(s => s.Asset == null
                || s.Asset.AssignedToUnitId == null
                || scope.VisibleUnitIds.Contains(s.Asset.AssignedToUnitId.Value));
        }

        var schedules = await query
            .OrderBy(s => s.NextScheduledDate)
            .ToListAsync();

        return View(schedules);
    }

    /// <summary>
    /// List all maintenance records
    /// </summary>
    public async Task<IActionResult> Records()
    {
        var query = _context.MaintenanceRecords
            .Include(r => r.Asset)
                .ThenInclude(a => a.Category)
            .AsQueryable();

        // FUNC-008: same Asset-derived scope filter as Schedules above.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.IsUnscoped && scope.VisibleUnitIds != null)
        {
            query = query.Where(r => r.Asset == null
                || r.Asset.AssignedToUnitId == null
                || scope.VisibleUnitIds.Contains(r.Asset.AssignedToUnitId.Value));
        }

        var records = await query
            .OrderByDescending(r => r.PerformedDate)
            .Take(100)
            .ToListAsync();

        return View(records);
    }

    /// <summary>
    /// Create maintenance schedule form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Asset.Create)]
    public async Task<IActionResult> CreateSchedule()
    {
        await PopulateDropdowns();
        var schedule = new MaintenanceSchedule
        {
            NextScheduledDate = DateTime.UtcNow.AddDays(30),
            FrequencyDays = 30,
            IsActive = true
        };
        return View(schedule);
    }

    /// <summary>
    /// Create maintenance schedule
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSchedule(MaintenanceSchedule schedule)
    {
        // FUNC-008: a scoped user must not attach a schedule to an asset outside
        // their unit by posting a foreign AssetId.
        if (!await CanAccessAssetAsync(schedule.AssetId))
            return NotFound();

        if (ModelState.IsValid)
        {
            schedule.Id = Guid.NewGuid().ToString();
            schedule.CreatedAt = DateTime.UtcNow;
            schedule.UpdatedAt = DateTime.UtcNow;
            schedule.CreatedById = User.Identity?.Name;

            _context.MaintenanceSchedules.Add(schedule);
            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_MaintenanceScheduleCreated"].Value;
            return RedirectToAction(nameof(Schedules));
        }

        await PopulateDropdowns();
        return View(schedule);
    }

    /// <summary>
    /// Edit maintenance schedule form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Asset.Edit)]
    public async Task<IActionResult> EditSchedule(string id)
    {
        var schedule = await _context.MaintenanceSchedules
            .Include(s => s.Asset)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        if (schedule == null)
            return NotFound();

        // FUNC-008: per-record IDOR guard via the linked Asset's unit.
        var scope = await _scopingService.GetScopeAsync(User);
        if (!scope.CanAccess(new AssetScopeProbe(schedule.Asset?.AssignedToUnitId)))
            return NotFound();

        await PopulateDropdowns();
        return View(schedule);
    }

    /// <summary>
    /// Edit maintenance schedule
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSchedule(string id, MaintenanceSchedule schedule)
    {
        if (id != schedule.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            // FUNC-001 mass-assignment defense + FUNC-008 scope guard: load the
            // tracked entity and copy only user-editable fields. _context.Update
            // wrote every column from the post, letting a crafted body flip
            // IsDeleted=true, forge CreatedById/CreatedAt/DeletedAt/Version, or
            // re-point AssetId to another unit's asset (scope escape). AssetId,
            // LastPerformedDate, AssignedToId and Instructions keep their
            // persisted values (the Edit form doesn't legitimately change them).
            var existing = await _context.MaintenanceSchedules
                .Include(s => s.Asset)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
            if (existing == null)
                return NotFound();

            // IDOR re-check against the *persisted* asset, not the posted one.
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(new AssetScopeProbe(existing.Asset?.AssignedToUnitId)))
                return NotFound();

            existing.Type                   = schedule.Type;
            existing.NameEn                 = schedule.NameEn;
            existing.NameAr                 = schedule.NameAr;
            existing.DescriptionEn          = schedule.DescriptionEn;
            existing.DescriptionAr          = schedule.DescriptionAr;
            existing.FrequencyDays          = schedule.FrequencyDays;
            existing.NextScheduledDate      = schedule.NextScheduledDate;
            existing.IsActive               = schedule.IsActive;
            existing.EstimatedDurationHours = schedule.EstimatedDurationHours;
            existing.EstimatedCost          = schedule.EstimatedCost;
            existing.UpdatedAt              = DateTime.UtcNow;
            existing.UpdatedById            = User.Identity?.Name;
            existing.Version++;

            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_MaintenanceScheduleUpdated"].Value;
            return RedirectToAction(nameof(Schedules));
        }

        await PopulateDropdowns();
        return View(schedule);
    }

    /// <summary>
    /// Create maintenance record form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Asset.Create)]
    public async Task<IActionResult> CreateRecord()
    {
        await PopulateDropdowns();
        var record = new MaintenanceRecord
        {
            PerformedDate = DateTime.UtcNow,
            IsCompleted = true
        };
        return View(record);
    }

    /// <summary>
    /// Create maintenance record
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRecord(MaintenanceRecord record)
    {
        // FUNC-008: a scoped user must not log a record against an asset outside
        // their unit by posting a foreign AssetId.
        if (!await CanAccessAssetAsync(record.AssetId))
            return NotFound();

        if (ModelState.IsValid)
        {
            record.Id = Guid.NewGuid().ToString();
            record.PerformedById = User.Identity?.Name;

            // Update the schedule's last-performed/next-due dates if linked — but
            // only when the schedule actually belongs to the same asset as this
            // record. FUNC-008: the old code mutated NextScheduledDate on whatever
            // schedule the (untrusted) MaintenanceScheduleId pointed at, so a
            // record on Asset A could silently reset Asset B's schedule. If the
            // selected schedule is for a different asset, reject the post rather
            // than corrupt an unrelated schedule.
            if (!string.IsNullOrEmpty(record.MaintenanceScheduleId))
            {
                var schedule = await _context.MaintenanceSchedules
                    .FirstOrDefaultAsync(s => s.Id == record.MaintenanceScheduleId && !s.IsDeleted);
                if (schedule == null)
                {
                    ModelState.AddModelError(nameof(MaintenanceRecord.MaintenanceScheduleId),
                        "The selected maintenance schedule no longer exists.");
                    await PopulateDropdowns();
                    return View(record);
                }
                if (schedule.AssetId != record.AssetId)
                {
                    ModelState.AddModelError(nameof(MaintenanceRecord.MaintenanceScheduleId),
                        "The selected schedule belongs to a different asset. Pick a schedule for the same asset, or leave it blank for ad-hoc maintenance.");
                    await PopulateDropdowns();
                    return View(record);
                }

                schedule.LastPerformedDate = record.PerformedDate;
                schedule.NextScheduledDate = record.PerformedDate.AddDays(schedule.FrequencyDays);
                schedule.UpdatedAt = DateTime.UtcNow;
            }

            _context.MaintenanceRecords.Add(record);
            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_MaintenanceRecordCreated"].Value;
            return RedirectToAction(nameof(Records));
        }

        await PopulateDropdowns();
        return View(record);
    }

    /// <summary>
    /// Delete maintenance schedule (soft delete)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSchedule(string id)
    {
        var schedule = await _context.MaintenanceSchedules
            .Include(s => s.Asset)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (schedule != null)
        {
            // FUNC-008: per-record IDOR guard via the linked Asset's unit.
            var scope = await _scopingService.GetScopeAsync(User);
            if (!scope.CanAccess(new AssetScopeProbe(schedule.Asset?.AssignedToUnitId)))
                return NotFound();

            schedule.IsDeleted = true;
            schedule.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = _localizer["Success_MaintenanceScheduleDeleted"].Value;
        }

        return RedirectToAction(nameof(Schedules));
    }

    private async Task PopulateDropdowns()
    {
        // FUNC-008: scope both pickers so a scoped user can only attach
        // maintenance to assets (and schedules) within their unit.
        var scope = await _scopingService.GetScopeAsync(User);

        var assetsQuery = _context.Assets.Where(a => !a.IsDeleted).AsQueryable();
        assetsQuery = assetsQuery.ApplyAssignedUnitScope(scope);
        ViewBag.Assets = new SelectList(
            await assetsQuery.OrderBy(a => a.AssetTag).ToListAsync(),
            "Id", "Name");

        var schedulesQuery = _context.MaintenanceSchedules
            .Where(s => !s.IsDeleted && s.IsActive)
            .Include(s => s.Asset)
            .AsQueryable();
        if (!scope.IsUnscoped && scope.VisibleUnitIds != null)
        {
            schedulesQuery = schedulesQuery.Where(s => s.Asset == null
                || s.Asset.AssignedToUnitId == null
                || scope.VisibleUnitIds.Contains(s.Asset.AssignedToUnitId.Value));
        }
        ViewBag.MaintenanceSchedules = new SelectList(
            await schedulesQuery.ToListAsync(),
            "Id", "Name");

        ViewBag.MaintenanceTypes = new SelectList(
            Enum.GetValues(typeof(MaintenanceType)).Cast<MaintenanceType>()
                .Select(e => new { Value = (int)e, Text = e.ToString() }),
            "Value", "Text");
    }
}


