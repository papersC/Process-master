using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Common;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for APQC Level 1 - Categories
/// </summary>
[Authorize(Policy = AppPolicies.Module.Process.View)]
public class CategoriesController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CategoriesController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly HierarchicalCodeService _codeSvc;

    public CategoriesController(ApplicationDbContext context, ILogger<CategoriesController> logger, IStringLocalizer<SharedResource> localizer, HierarchicalCodeService codeSvc)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
        _codeSvc = codeSvc;
    }

    /// <summary>
    /// List all categories
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .Where(c => !c.IsDeleted)
            .Include(c => c.ProcessGroups.Where(pg => !pg.IsDeleted))
                .ThenInclude(pg => pg.Processes.Where(p => !p.IsDeleted))
            .OrderBy(c => c.SortKey ?? c.Code)
            .ToListAsync();

        return View(categories);
    }

    /// <summary>
    /// View category details
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var category = await _context.Categories
            .Include(c => c.ProcessGroups.Where(pg => !pg.IsDeleted))
                .ThenInclude(pg => pg.Processes.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (category == null)
            return NotFound();

        return View(category);
    }

    /// <summary>
    /// Create category form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    public IActionResult Create()
    {
        return View(new Category());
    }

    /// <summary>
    /// Create category
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        // Code is system-managed: the user doesn't see it on the form. Clear
        // any accidental ModelState errors from prior validation wiring
        // before the IsValid check.
        ModelState.Remove(nameof(Category.Code));

        if (ModelState.IsValid)
        {
            await _codeSvc.AllocateWithRetryAsync(async () =>
            {
                category.Id = Guid.NewGuid().ToString();
                category.Code = await _codeSvc.NextCategoryCodeAsync();
                category.SortKey = HierarchicalCodeService.SortKeyFor(category.Code);
                category.CreatedAt = DateTime.UtcNow;
                category.UpdatedAt = DateTime.UtcNow;
                category.CreatedById = User.Identity?.Name;

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                return true;
            });

            TempData["Success"] = _localizer["Success_CategoryCreated"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View(category);
    }

    /// <summary>
    /// Edit category form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null || category.IsDeleted)
            return NotFound();

        return View(category);
    }

    /// <summary>
    /// Edit category
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Category category)
    {
        if (id != category.Id)
            return NotFound();

        // Code is system-managed; never overwrite it from the form. Patch the
        // editable fields onto the existing row.
        ModelState.Remove(nameof(Category.Code));

        if (ModelState.IsValid)
        {
            var existing = await _context.Categories.FindAsync(id);
            if (existing == null || existing.IsDeleted) return NotFound();

            existing.NameEn = category.NameEn;
            existing.NameAr = category.NameAr;
            existing.DescriptionEn = category.DescriptionEn;
            existing.DescriptionAr = category.DescriptionAr;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedById = User.Identity?.Name;
            existing.Version++;

            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_CategoryUpdated"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View(category);
    }

    /// <summary>
    /// Delete category (soft delete)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.CanAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound();

        // In-use guard. Without this, deleting a Category silently soft-deletes
        // the parent but leaves ProcessGroups + Processes pointing at a now-
        // hidden FK. The Index page then shows orphaned groups with a label that
        // resolves to nothing. Surface a friendly error and bail.
        var groupCount = await _context.ProcessGroups
            .CountAsync(g => g.CategoryId == id && !g.IsDeleted);
        var processCount = await _context.Processes
            .CountAsync(p => p.ProcessGroup != null
                          && p.ProcessGroup.CategoryId == id
                          && !p.IsDeleted);
        if (groupCount > 0 || processCount > 0)
        {
            TempData["Error"] = string.Format(
                _localizer["Error_CategoryInUse"].Value,
                groupCount, processCount);
            return RedirectToAction(nameof(Index));
        }

        category.IsDeleted = true;
        category.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_CategoryDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

}

