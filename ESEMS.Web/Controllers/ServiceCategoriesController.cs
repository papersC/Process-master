using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// CRUD for the ServiceCategory lookup. Flat list; mirrors the pattern of
/// AssetCategoriesController but without the hierarchy and asset-specific
/// defaults. Delete has an in-use guard against the new Service.ServiceCategoryId FK.
/// </summary>
[Authorize(Policy = AppPolicies.Module.Service.View)]
public class ServiceCategoriesController : BaseController
{
    private readonly ApplicationDbContext _context;

    public ServiceCategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _context.ServiceCategories
            .Where(c => !c.IsDeleted)
            .Include(c => c.Services.Where(s => !s.IsDeleted))
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Code)
            .ToListAsync();
        return View(categories);
    }

    public async Task<IActionResult> Details(string id)
    {
        var category = await _context.ServiceCategories
            .Include(c => c.Services.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (category == null) return NotFound();
        return View(category);
    }

    [Authorize(Policy = AppPolicies.Module.Service.Create)]
    public async Task<IActionResult> Create()
    {
        return View(new ServiceCategory
        {
            Code = await GenerateCodeAsync(),
            IsActive = true
        });
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Service.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceCategory model)
    {
        // FUNC-017: Code is a server-managed, read-only value (the form input is
        // a read-only placeholder). The old shape trusted a posted Code when one
        // was present, with no uniqueness check — a crafted POST could set an
        // arbitrary or duplicate code. Always regenerate and ignore the posted
        // value; strip it from ModelState so it can't interfere with validation.
        // GenerateCodeAsync() returns SC-{max+1}, unique by construction.
        ModelState.Remove(nameof(ServiceCategory.Code));

        if (ModelState.IsValid)
        {
            model.Id = Guid.NewGuid().ToString();
            model.Code = await GenerateCodeAsync();
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            model.CreatedById = User.Identity?.Name;
            _context.ServiceCategories.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Service category created.";
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }

    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);
        if (category == null || category.IsDeleted) return NotFound();
        return View(category);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Service.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ServiceCategory model)
    {
        if (id != model.Id) return NotFound();
        if (ModelState.IsValid)
        {
            var existing = await _context.ServiceCategories.FindAsync(id);
            if (existing == null) return NotFound();
            existing.Code = model.Code?.Trim() ?? existing.Code;
            existing.NameEn = model.NameEn;
            existing.NameAr = model.NameAr;
            existing.DescriptionEn = model.DescriptionEn;
            existing.DescriptionAr = model.DescriptionAr;
            existing.DisplayOrder = model.DisplayOrder;
            existing.IsActive = model.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedById = User.Identity?.Name;
            existing.Version++;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Service category updated.";
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Service.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);
        if (category == null) return NotFound();

        var inUse = await _context.Services
            .CountAsync(s => s.ServiceCategoryId == id && !s.IsDeleted);
        if (inUse > 0)
        {
            TempData["Error"] = $"Cannot delete — {inUse} service(s) are still classified under this category. Reassign them first.";
            return RedirectToAction(nameof(Index));
        }

        category.IsDeleted = true;
        category.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Service category deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateCodeAsync()
    {
        var codes = await _context.ServiceCategories
            .Where(c => c.Code.StartsWith("SC-"))
            .Select(c => c.Code)
            .ToListAsync();
        var max = codes
            .Select(c =>
            {
                var parts = c.Split('-');
                return parts.Length == 2 && int.TryParse(parts[1], out var n) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();
        return $"SC-{(max + 1):D3}";
    }
}
