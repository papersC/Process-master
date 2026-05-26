using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for APQC Level 4 - Activities
/// </summary>
[Authorize(Policy = AppPolicies.Module.Process.View)]
public class ActivitiesController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ActivitiesController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ActivitiesController(ApplicationDbContext context, ILogger<ActivitiesController> logger, IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    // Activities are scoped to a parent Process. /Activities bare has no
    // meaningful list — route users to Processes so a stale URL doesn't 404.
    public IActionResult Index() => RedirectToAction("Index", "Processes");

    /// <summary>
    /// Create activity form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    public async Task<IActionResult> Create(string? processId = null)
    {
        await PopulateDropdowns();
        var model = new Activity();
        if (!string.IsNullOrEmpty(processId))
        {
            model.ProcessId = processId;
            // Auto-generate next code — hierarchical convention:
            // {ProcessCode}.01, {ProcessCode}.02, ... (zero-padded to 2
            // digits so codes sort lexicographically). Matches the wizard
            // path in ProcessesController and the help text shown to users.
            var process = await _context.Processes.FindAsync(processId);
            if (process != null)
            {
                var existingCount = await _context.Activities
                    .CountAsync(a => a.ProcessId == processId && !a.IsDeleted);
                model.Code = $"{process.Code}.{(existingCount + 1):D2}";
                model.DisplayOrder = existingCount + 1;
            }
        }
        return View(model);
    }

    /// <summary>
    /// Create activity
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity activity)
    {
        if (ModelState.IsValid)
        {
            activity.Id = Guid.NewGuid().ToString();
            activity.CreatedAt = DateTime.UtcNow;
            activity.UpdatedAt = DateTime.UtcNow;
            activity.CreatedById = User.Identity?.Name;

            // DisplayOrder is auto-assigned (form input is hidden). If the GET pre-fill
            // didn't run or the value is blank, take the next available within the parent process.
            if (activity.DisplayOrder <= 0)
            {
                var nextOrder = await _context.Activities
                    .Where(a => a.ProcessId == activity.ProcessId && !a.IsDeleted)
                    .MaxAsync(a => (int?)a.DisplayOrder) ?? 0;
                activity.DisplayOrder = nextOrder + 1;
            }

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Activity {Code} created by {User}", activity.Code, User.Identity?.Name);
            TempData["Success"] = _localizer["Success_ActivityCreated"].Value;
            return RedirectToAction("Details", "Processes", new { id = activity.ProcessId });
        }

        await PopulateDropdowns();
        return View(activity);
    }

    /// <summary>
    /// Edit activity form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var activity = await _context.Activities
            .Include(a => a.Process)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        if (activity == null)
            return NotFound();

        await PopulateDropdowns();
        return View(activity);
    }

    /// <summary>
    /// Edit activity
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Activity activity)
    {
        if (id != activity.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            // FUNC-001 mass-assignment defense: load the tracked entity and copy
            // only user-editable fields. _context.Update(activity) overwrote every
            // column from the post — a crafted body could flip IsDeleted=true or
            // forge CreatedById/CreatedAt/DeletedAt/Version. Code and DisplayOrder
            // are server-managed (Code is read-only on the form per F-PA-011) and
            // must keep their persisted values.
            var existing = await _context.Activities
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (existing == null)
                return NotFound();

            existing.ProcessId            = activity.ProcessId;
            existing.NameEn               = activity.NameEn;
            existing.NameAr               = activity.NameAr;
            existing.DescriptionEn        = activity.DescriptionEn;
            existing.DescriptionAr        = activity.DescriptionAr;
            existing.ChannelType          = activity.ChannelType;
            existing.OwningUnitId         = activity.OwningUnitId;
            existing.Tags                 = activity.Tags;
            existing.EstimatedDuration    = activity.EstimatedDuration;
            existing.DurationUnit         = activity.DurationUnit;
            existing.EstimatedCost        = activity.EstimatedCost;
            existing.IsAutomated          = activity.IsAutomated;
            existing.HasDetailedBreakdown = activity.HasDetailedBreakdown;
            existing.UpdatedAt            = DateTime.UtcNow;
            existing.UpdatedById          = User.Identity?.Name;
            existing.Version++;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Activity {Code} updated by {User}", existing.Code, User.Identity?.Name);
            TempData["Success"] = _localizer["Success_ActivityUpdated"].Value;
            return RedirectToAction("Details", "Processes", new { id = existing.ProcessId });
        }

        await PopulateDropdowns();
        return View(activity);
    }

    /// <summary>
    /// Delete activity (soft delete)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var activity = await _context.Activities
            .Include(a => a.Tasks)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

        if (activity == null)
            return NotFound();

        // Soft delete activity and its tasks
        activity.IsDeleted = true;
        activity.DeletedAt = DateTime.UtcNow;
        foreach (var task in activity.Tasks.Where(t => !t.IsDeleted))
        {
            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Activity {Code} deleted by {User}", activity.Code, User.Identity?.Name);
        TempData["Success"] = _localizer["Success_ActivityDeleted"].Value;
        return RedirectToAction("Details", "Processes", new { id = activity.ProcessId });
    }

    private async Task PopulateDropdowns()
    {
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");

        var processes = await _context.Processes
            .Where(p => !p.IsDeleted)
            .Include(p => p.ProcessGroup)
            .OrderBy(p => p.Code)
            .ToListAsync();
        ViewBag.Processes = new SelectList(
            processes.Select(p => new { p.Id, DisplayName = $"{p.Code} - {(isArabic ? p.NameAr : p.NameEn)}" }),
            "Id", "DisplayName");

        var orgUnits = await _context.OrganizationUnits
            .Where(u => !u.IsDeleted && u.IsActive)
            .OrderBy(u => u.Level).ThenBy(u => u.NameEn)
            .ToListAsync();
        ViewBag.OrganizationUnits = new SelectList(
            orgUnits.Select(u => new { u.Id, DisplayName = isArabic ? u.NameAr : u.NameEn }),
            "Id", "DisplayName");
    }
}
