using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Admin CRUD for the <see cref="SystemDefinition"/> lookup — the IT systems
/// that populate the "System" dropdown on processes and tasks. Mirrors
/// ServiceCategoriesController: flat list, server-generated Code, soft-delete
/// with an in-use guard against Process.SystemId / ProcessTask.SystemId.
/// Lives under the Administration menu (Settings policy).
/// </summary>
[Authorize(Policy = AppPolicies.Module.Settings.View)]
public class SystemsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public SystemsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var systems = await _context.SystemDefinitions
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.NameEn)
            .ToListAsync();

        // How many processes + tasks point at each system — shown in the list
        // and used to block deletes of an in-use system. Two grouped queries.
        var ids = systems.Select(s => s.Id).ToList();
        var procCounts = await _context.Processes
            .Where(p => !p.IsDeleted && p.SystemId != null && ids.Contains(p.SystemId))
            .GroupBy(p => p.SystemId!)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        var taskCounts = await _context.ProcessTasks
            .Where(t => !t.IsDeleted && t.SystemId != null && ids.Contains(t.SystemId))
            .GroupBy(t => t.SystemId!)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        ViewBag.Usage = ids.ToDictionary(
            id => id,
            id => (procCounts.FirstOrDefault(x => x.Id == id)?.Count ?? 0)
                + (taskCounts.FirstOrDefault(x => x.Id == id)?.Count ?? 0));

        return View(systems);
    }

    [Authorize(Policy = AppPolicies.Module.Settings.Edit)]
    public async Task<IActionResult> Create()
    {
        return View(new SystemDefinition { Code = await GenerateCodeAsync(), IsActive = true });
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Settings.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SystemDefinition model)
    {
        // Code is server-managed (the form field is read-only). Always
        // regenerate so a crafted POST can't set an arbitrary/duplicate code.
        ModelState.Remove(nameof(SystemDefinition.Code));

        if (ModelState.IsValid)
        {
            model.Id = Guid.NewGuid().ToString();
            model.Code = await GenerateCodeAsync();
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            model.CreatedById = User.Identity?.Name;
            _context.SystemDefinitions.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "System created.";
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }

    [Authorize(Policy = AppPolicies.Module.Settings.Edit)]
    public async Task<IActionResult> Edit(string id)
    {
        var system = await _context.SystemDefinitions.FindAsync(id);
        if (system == null || system.IsDeleted) return NotFound();
        return View(system);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Settings.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, SystemDefinition model)
    {
        if (id != model.Id) return NotFound();
        if (ModelState.IsValid)
        {
            var existing = await _context.SystemDefinitions.FindAsync(id);
            if (existing == null || existing.IsDeleted) return NotFound();

            existing.NameEn = model.NameEn;
            existing.NameAr = model.NameAr;
            existing.DescriptionEn = model.DescriptionEn;
            existing.DescriptionAr = model.DescriptionAr;
            existing.SystemType = model.SystemType;
            existing.Vendor = model.Vendor;
            existing.SystemVersion = model.SystemVersion;
            existing.Url = model.Url;
            existing.SupportContact = model.SupportContact;
            existing.DisplayOrder = model.DisplayOrder;
            existing.IsActive = model.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedById = User.Identity?.Name;
            existing.Version++;
            await _context.SaveChangesAsync();

            TempData["Success"] = "System updated.";
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.Settings.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var system = await _context.SystemDefinitions.FindAsync(id);
        if (system == null) return NotFound();

        var procUse = await _context.Processes.CountAsync(p => p.SystemId == id && !p.IsDeleted);
        var taskUse = await _context.ProcessTasks.CountAsync(t => t.SystemId == id && !t.IsDeleted);
        if (procUse + taskUse > 0)
        {
            TempData["Error"] = $"Cannot delete — {procUse + taskUse} process(es)/task(s) still use this system. Reassign them first.";
            return RedirectToAction(nameof(Index));
        }

        system.IsDeleted = true;
        system.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        TempData["Success"] = "System deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Generates the next SYS-NNN code, unique by construction.</summary>
    private async Task<string> GenerateCodeAsync()
    {
        var codes = await _context.SystemDefinitions
            .Where(s => s.Code.StartsWith("SYS-"))
            .Select(s => s.Code)
            .ToListAsync();
        var max = codes
            .Select(c =>
            {
                var parts = c.Split('-');
                return parts.Length == 2 && int.TryParse(parts[1], out var n) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();
        return $"SYS-{(max + 1):D3}";
    }
}
