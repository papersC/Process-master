using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Admin controller for the org-wide JobPosition catalog — the positions used by
/// RACI rows to point at a specific role within an organization unit (e.g.
/// "the Director of إدارة المشاريع الهندسية"). See <see cref="JobPosition"/> for
/// the rationale on org-wide vs unit-scoped roles.
/// </summary>
[Authorize(Policy = AppPolicies.CanAdmin)]
public class JobPositionsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<JobPositionsController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public JobPositionsController(ApplicationDbContext context, ILogger<JobPositionsController> logger, IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _context.JobPositions
            .Where(j => !j.IsDeleted)
            .OrderBy(j => j.DisplayOrder)
            .ThenBy(j => j.NameEn)
            .ToListAsync();
        return View(roles);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateUnitsAsync();
        return View(new JobPosition());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JobPosition role)
    {
        if (!ModelState.IsValid)
        {
            await PopulateUnitsAsync();
            return View(role);
        }

        role.Id = Guid.NewGuid().ToString();
        role.CreatedAt = DateTime.UtcNow;
        role.UpdatedAt = DateTime.UtcNow;
        role.CreatedById = User.Identity?.Name;
        // OrganizationUnitId is an int? FK now — an unselected dropdown binds to
        // null already, so no empty-string normalization is needed.
        _context.JobPositions.Add(role);
        await _context.SaveChangesAsync();

        TempData["Success"] = IsArabic() ? "تم الحفظ بنجاح" : "Saved successfully";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var role = await _context.JobPositions.FindAsync(id);
        if (role == null || role.IsDeleted) return NotFound();
        await PopulateUnitsAsync();
        return View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, JobPosition role)
    {
        if (id != role.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateUnitsAsync();
            return View(role);
        }

        var existing = await _context.JobPositions.FindAsync(id);
        if (existing == null || existing.IsDeleted) return NotFound();

        existing.Code = role.Code;
        existing.NameEn = role.NameEn;
        existing.NameAr = role.NameAr;
        existing.DescriptionEn = role.DescriptionEn;
        existing.DescriptionAr = role.DescriptionAr;
        existing.Category = role.Category;
        existing.IsLeadership = role.IsLeadership;
        existing.DisplayOrder = role.DisplayOrder;
        existing.OrganizationUnitId = role.OrganizationUnitId;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedById = User.Identity?.Name;
        existing.Version++;
        await _context.SaveChangesAsync();

        TempData["Success"] = IsArabic() ? "تم الحفظ بنجاح" : "Saved successfully";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _context.JobPositions.FindAsync(id);
        if (role == null) return NotFound();

        // In-use guard — if any RACI row references this role, refuse the
        // delete and tell the user how many entries are blocking it.
        var processRaciCount = await _context.ProcessRacis.CountAsync(r => r.JobPositionId == id);
        var activityRaciCount = await _context.ActivityRacis.CountAsync(r => r.JobPositionId == id);
        var taskRaciCount = await _context.TaskRacis.CountAsync(r => r.JobPositionId == id);
        var total = processRaciCount + activityRaciCount + taskRaciCount;
        if (total > 0)
        {
            TempData["Error"] = IsArabic()
                ? $"لا يمكن الحذف — {total} سجل مصفوفة RACI ما زالت تشير إلى هذا الدور."
                : $"Cannot delete — {total} RACI entries still reference this role.";
            return RedirectToAction(nameof(Index));
        }

        role.IsDeleted = true;
        role.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = IsArabic() ? "تم الحذف" : "Deleted";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateUnitsAsync()
    {
        // Only active, non-deleted units. Ordered by code so picker matches
        // the OrganizationUnits Index page ordering.
        ViewBag.OrgUnits = await _context.OrganizationUnits
            .Where(u => !u.IsDeleted && u.IsActive)
            .OrderBy(u => u.Code)
            .Select(u => new { u.Id, NameEn = u.NameEn, NameAr = u.NameAr })
            .ToListAsync();
    }

    private static bool IsArabic() =>
        System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
}
