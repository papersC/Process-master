using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for Asset Categories Management (ISO 55001:2014)
/// Manages asset classification hierarchy and category defaults
/// </summary>
[Authorize(Policy = AppPolicies.Module.Asset.View)]
public class AssetCategoriesController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AssetCategoriesController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AssetCategoriesController(ApplicationDbContext context, ILogger<AssetCategoriesController> logger, IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// List all asset categories
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var categories = await _context.AssetCategories
            .Where(c => !c.IsDeleted)
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
            .Include(c => c.Assets.Where(a => !a.IsDeleted))
            .OrderBy(c => c.Code)
            .ToListAsync();

        return View(categories);
    }

    /// <summary>
    /// View asset category details
    /// </summary>
    public async Task<IActionResult> Details(string id)
    {
        var category = await _context.AssetCategories
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
            .Include(c => c.Assets.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.AssignedToUnit)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (category == null)
            return NotFound();

        return View(category);
    }

    /// <summary>
    /// Create asset category form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Asset.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        var category = new AssetCategory
        {
            Code = await GenerateCategoryCode()
        };
        return View(category);
    }

    /// <summary>
    /// Create asset category
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AssetCategory category)
    {
        // FUNC-017: Code is a server-managed, read-only value. The form posts the
        // GET-time placeholder, but we always regenerate on save; strip it from
        // ModelState so the posted value is fully ignored (no validation
        // interference, no silent mismatch between the shown and the saved code).
        ModelState.Remove(nameof(AssetCategory.Code));

        if (ModelState.IsValid)
        {
            category.Id = Guid.NewGuid().ToString();
            category.Code = await GenerateCategoryCode();
            category.CreatedAt = DateTime.UtcNow;
            category.UpdatedAt = DateTime.UtcNow;
            category.CreatedById = User.Identity?.Name;

            _context.AssetCategories.Add(category);
            await _context.SaveChangesAsync();

            // FUNC-010: layout toast reads TempData["Success"]/["Error"] — using
            // SuccessMessage/ErrorMessage silently swallowed these messages.
            TempData["Success"] = _localizer["Success_AssetCategoryCreated"].Value;
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns();
        return View(category);
    }

    /// <summary>
    /// Edit asset category form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.Asset.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var category = await _context.AssetCategories.FindAsync(id);
        if (category == null || category.IsDeleted)
            return NotFound();

        await PopulateDropdowns(excludeId: id);
        return View(category);
    }

    /// <summary>
    /// Edit asset category
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, AssetCategory category)
    {
        if (id != category.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            category.UpdatedAt = DateTime.UtcNow;
            category.UpdatedById = User.Identity?.Name;
            category.Version++;

            _context.Update(category);
            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_AssetCategoryUpdated"].Value;
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(excludeId: id);
        return View(category);
    }

    /// <summary>
    /// Delete asset category (soft delete)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Asset.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var category = await _context.AssetCategories.FindAsync(id);
        if (category == null)
            return NotFound();

        // Check if category has assets
        var hasAssets = await _context.Assets.AnyAsync(a => a.CategoryId == id && !a.IsDeleted);
        if (hasAssets)
        {
            // FUNC-010: standardized on TempData["Error"] — the layout toast
            // never read TempData["ErrorMessage"], so this guard's reason was
            // invisible (the user just bounced to Details with no explanation).
            TempData["Error"] = _localizer["Error_CategoryHasAssets"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Self-reference guard: an AssetCategory can parent other AssetCategories
        // (e.g. AST-RE → AST-RE-VILLA). Deleting the parent without unhooking
        // children leaves the children orphaned, same regression class as the
        // Categories one. Uses TempData["Error"] (layout-handled toast) because
        // the Details page is no longer reachable for a deleted category.
        var subCategoryCount = await _context.AssetCategories
            .CountAsync(c => c.ParentCategoryId == id && !c.IsDeleted);
        if (subCategoryCount > 0)
        {
            TempData["Error"] = string.Format(
                _localizer["Error_AssetCategoryHasSubCategories"].Value,
                subCategoryCount);
            return RedirectToAction(nameof(Index));
        }

        category.IsDeleted = true;
        category.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_AssetCategoryDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Builds the Parent Category dropdown. Excludes the category being
    /// edited (audit finding M8) so a user can't accidentally set a row
    /// as its own parent. Doesn't yet exclude descendants — multi-level
    /// cycle detection is a follow-up.
    /// </summary>
    private async Task PopulateDropdowns(string? excludeId = null)
    {
        var parentCategories = await _context.AssetCategories
            .Where(c => !c.IsDeleted && (excludeId == null || c.Id != excludeId))
            .OrderBy(c => c.Code)
            .ToListAsync();

        ViewBag.ParentCategories = new SelectList(parentCategories, "Id", "Name");
    }

    private async Task<string> GenerateCategoryCode()
    {
        // Get all categories that follow the AC-### format
        var categories = await _context.AssetCategories
            .Where(c => c.Code.StartsWith("AC-"))
            .ToListAsync();

        if (!categories.Any())
            return "AC-001";

        // Parse numeric codes and find the highest number.
        // Single-pass TryParse — the previous shape did TryParse then Parse
        // which double-parses and re-opens a crash window if data changes
        // between the two calls (or if someone edits the Where predicate
        // without also fixing the Select).
        var maxNumber = categories
            .Select(c =>
            {
                var parts = c.Code.Split('-');
                return (parts.Length == 2 && int.TryParse(parts[1], out var n)) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return $"AC-{(maxNumber + 1):D3}";
    }

    private async Task<bool> CategoryExists(string id)
    {
        return await _context.AssetCategories.AnyAsync(c => c.Id == id && !c.IsDeleted);
    }
}


