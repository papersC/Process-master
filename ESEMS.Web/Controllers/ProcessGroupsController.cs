using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for APQC Level 2 - Process Groups
/// </summary>
[Authorize(Policy = AppPolicies.Module.Process.View)]
public class ProcessGroupsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProcessGroupsController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ESEMS.Web.Services.Common.HierarchicalCodeService _codeSvc;

    public ProcessGroupsController(ApplicationDbContext context, ILogger<ProcessGroupsController> logger, IStringLocalizer<SharedResource> localizer, ESEMS.Web.Services.Common.HierarchicalCodeService codeSvc)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _codeSvc = codeSvc;
    }

    /// <summary>
    /// List all process groups
    /// </summary>
    public async Task<IActionResult> Index(string? categoryId = null)
    {
        var query = _context.ProcessGroups
            .Where(pg => !pg.IsDeleted)
            .Include(pg => pg.Category)
            .Include(pg => pg.Processes.Where(p => !p.IsDeleted))
            .AsQueryable();

        if (!string.IsNullOrEmpty(categoryId))
            query = query.Where(pg => pg.CategoryId == categoryId);

        // Null-safe sort: process groups with no Category are pushed to the
        // end. Sort by parent Category's SortKey then own SortKey for natural
        // numeric ordering of hierarchical codes (1.1 before 1.10 before 2.1).
        var processGroups = await query
            .OrderBy(pg => pg.Category == null ? "~~~" : (pg.Category.SortKey ?? pg.Category.Code))
            .ThenBy(pg => pg.SortKey ?? pg.Code)
            .ToListAsync();

        ViewBag.Categories = await _context.Categories.Where(c => !c.IsDeleted).ToListAsync();
        ViewBag.SelectedCategoryId = categoryId;

        return View(processGroups);
    }

    /// <summary>
    /// View process group details
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var processGroup = await _context.ProcessGroups
            .Include(pg => pg.Category)
            .Include(pg => pg.Processes.Where(p => !p.IsDeleted))
                .ThenInclude(p => p.Activities.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(pg => pg.Id == id && !pg.IsDeleted);

        if (processGroup == null)
            return NotFound();

        return View(processGroup);
    }

    /// <summary>
    /// Create process group form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    public async Task<IActionResult> Create(string? categoryId = null)
    {
        await PopulateDropdowns();
        var model = new ProcessGroup();
        if (!string.IsNullOrEmpty(categoryId))
            model.CategoryId = categoryId;
        return View(model);
    }

    /// <summary>
    /// Create process group
    /// </summary>
    /// <summary>
    /// AJAX preview — returns the next ProcessGroup code under the given Category in
    /// the same {Cat.Code}.{Y} shape the POST handler will allocate. Lets the Create
    /// view show "1.5" in the read-only badge as soon as the user picks a Category,
    /// without having to submit. Empty/unknown categoryId returns code: null.
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    public async Task<IActionResult> NextCode(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return Json(new { code = (string?)null });
        try
        {
            var code = await _codeSvc.NextProcessGroupCodeAsync(categoryId);
            return Json(new { code });
        }
        catch (InvalidOperationException)
        {
            // Category id was junk — UI just shows "—".
            return Json(new { code = (string?)null });
        }
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProcessGroup processGroup)
    {
        // Code is system-generated under the parent Category — clear any
        // posted/empty value and the Required-field validation error.
        ModelState.Remove(nameof(ProcessGroup.Code));

        if (ModelState.IsValid)
        {
            await _codeSvc.AllocateWithRetryAsync(async () =>
            {
                processGroup.Id = Guid.NewGuid().ToString();
                processGroup.Code = await _codeSvc.NextProcessGroupCodeAsync(processGroup.CategoryId);
                processGroup.SortKey = ESEMS.Web.Services.Common.HierarchicalCodeService.SortKeyFor(processGroup.Code);
                processGroup.CreatedAt = DateTime.UtcNow;
                processGroup.UpdatedAt = DateTime.UtcNow;
                processGroup.CreatedById = User.Identity?.Name;

                _context.ProcessGroups.Add(processGroup);
                await _context.SaveChangesAsync();
                return true;
            });

            TempData["Success"] = _localizer["Success_ProcessGroupCreated"].Value;
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns();
        return View(processGroup);
    }

    /// <summary>
    /// Edit process group form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var processGroup = await _context.ProcessGroups.FindAsync(id);
        if (processGroup == null || processGroup.IsDeleted)
            return NotFound();

        await PopulateDropdowns();
        return View(processGroup);
    }

    /// <summary>
    /// Edit process group
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ProcessGroup processGroup)
    {
        if (id != processGroup.Id)
            return NotFound();

        // Code is system-generated; never read it from the form.
        ModelState.Remove(nameof(ProcessGroup.Code));

        if (ModelState.IsValid)
        {
            // FUNC-001: load-then-patch instead of _context.Update(boundEntity).
            // A model-bound entity lets a crafted POST forge IsDeleted /
            // CreatedById / CreatedAt / Version / DeletedAt. Load the tracked
            // row and copy only user-editable fields; Code, SortKey and the
            // aggregated rollups stay system-managed.
            var existing = await _context.ProcessGroups
                .FirstOrDefaultAsync(pg => pg.Id == id && !pg.IsDeleted);
            if (existing == null)
                return NotFound();

            // Re-parenting to a different Category invalidates this group's
            // hierarchical Code ("{Cat.Code}.{Y}") and every descendant code —
            // capture the move BEFORE overwriting CategoryId so we can re-stamp
            // the subtree below. A same-category edit leaves Code untouched.
            var categoryChanged = existing.CategoryId != processGroup.CategoryId;

            existing.NameEn = processGroup.NameEn;
            existing.NameAr = processGroup.NameAr;
            existing.DescriptionEn = processGroup.DescriptionEn;
            existing.DescriptionAr = processGroup.DescriptionAr;
            existing.CategoryId = processGroup.CategoryId;
            existing.Tags = processGroup.Tags;
            existing.HasAutomatedProcesses = processGroup.HasAutomatedProcesses;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedById = User.Identity?.Name;
            existing.Version++;

            // Group moved to a new Category — re-stamp its Code/SortKey and
            // cascade the rename down to child Process/Activity/Task codes so the
            // whole subtree reflects the new parent. Runs before the single
            // SaveChanges so the rename commits atomically.
            if (categoryChanged)
                await _codeSvc.RecodeProcessGroupUnderCategoryAsync(existing);

            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_ProcessGroupUpdated"].Value;
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns();
        return View(processGroup);
    }

    /// <summary>
    /// Delete process group (soft delete)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var processGroup = await _context.ProcessGroups.FindAsync(id);
        if (processGroup == null)
            return NotFound();

        // In-use guard — see CategoriesController.Delete for rationale.
        var processCount = await _context.Processes
            .CountAsync(p => p.ProcessGroupId == id && !p.IsDeleted);
        if (processCount > 0)
        {
            TempData["Error"] = string.Format(
                _localizer["Error_ProcessGroupInUse"].Value,
                processCount);
            return RedirectToAction(nameof(Index));
        }

        processGroup.IsDeleted = true;
        processGroup.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_ProcessGroupDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(),
            "Id", "Name");
    }
}

