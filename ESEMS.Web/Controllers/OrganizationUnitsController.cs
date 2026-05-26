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
/// Controller for Organization Units management
/// </summary>
[Authorize(Policy = AppPolicies.Module.OrganizationUnit.View)]
public class OrganizationUnitsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrganizationUnitsController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public OrganizationUnitsController(ApplicationDbContext context, ILogger<OrganizationUnitsController> logger, IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// List all organization units (hierarchical)
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var units = await _context.OrganizationUnits
            .Where(u => !u.IsDeleted && u.ParentId == null)
            .Include(u => u.Children.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Children.Where(cc => !cc.IsDeleted))
                    .ThenInclude(cc => cc.Children.Where(ccc => !ccc.IsDeleted))
            .OrderBy(u => u.DisplayOrder)
            .ToListAsync();

        return View(units);
    }

    /// <summary>
    /// View organization unit details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var unit = await _context.OrganizationUnits
            .Include(u => u.Parent)
            .Include(u => u.Children.Where(c => !c.IsDeleted))
            .Include(u => u.OwnedProcesses.Where(p => !p.IsDeleted))
                .ThenInclude(p => p.ProcessGroup)
            .Include(u => u.OwnedResponsibilities.Where(r => !r.IsDeleted))
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

        if (unit == null)
            return NotFound();

        // Descendant rollup — "what does my sector own (counting children)?"
        // Walk the unit subtree once, collect every descendant Id, then run
        // three count queries against the full set. This makes a top-level
        // unit useful at a glance even when it owns nothing directly.
        var descendantIds = new HashSet<int> { id };
        var queue = new Queue<int>();
        queue.Enqueue(id);
        var allUnits = await _context.OrganizationUnits
            .Where(u => !u.IsDeleted)
            .Select(u => new { u.Id, u.ParentId })
            .ToListAsync();
        var byParent = allUnits.Where(u => u.ParentId != null)
            .GroupBy(u => u.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            if (!byParent.TryGetValue(parent, out var kids)) continue;
            foreach (var k in kids)
            {
                if (descendantIds.Add(k)) queue.Enqueue(k);
            }
        }
        descendantIds.Remove(id); // children-only counts
        ViewBag.DescendantProcessCount = descendantIds.Count == 0 ? 0
            : await _context.Processes
                .CountAsync(p => p.OwningUnitId != null && descendantIds.Contains(p.OwningUnitId.Value) && !p.IsDeleted);
        ViewBag.DescendantActivityCount = descendantIds.Count == 0 ? 0
            : await _context.Activities
                .CountAsync(a => a.OwningUnitId != null && descendantIds.Contains(a.OwningUnitId.Value) && !a.IsDeleted);
        ViewBag.DescendantTaskCount = descendantIds.Count == 0 ? 0
            : await _context.ProcessTasks
                .CountAsync(t => t.OwningUnitId != null && descendantIds.Contains(t.OwningUnitId.Value) && !t.IsDeleted);

        // Reverse-linked entities ("what does this unit own?"). Loaded as
        // separate queries because the OrganizationUnit nav collections
        // don't include the Process/Activity context we need for display.
        // Capped at 200 each — anything past that gets a "View all" link.
        ViewBag.OwnedActivities = await _context.Activities
            .Where(a => a.OwningUnitId == id && !a.IsDeleted)
            .Include(a => a.Process)
            .OrderBy(a => a.Process!.Code).ThenBy(a => a.DisplayOrder)
            .Take(200)
            .ToListAsync();

        ViewBag.OwnedTasks = await _context.ProcessTasks
            .Where(t => t.OwningUnitId == id && !t.IsDeleted)
            .Include(t => t.Activity)
                .ThenInclude(a => a!.Process)
            .OrderBy(t => t.Activity!.Process!.Code).ThenBy(t => t.DisplayOrder)
            .Take(200)
            .ToListAsync();

        // RACI roles this unit holds (across all three tiers). Shows the
        // operational responsibilities the unit carries beyond pure ownership.
        ViewBag.RaciProcessRoles = await _context.ProcessRacis
            .Where(r => r.OrganizationUnitId == id)
            .Include(r => r.Process)
            .OrderBy(r => r.Role).ThenBy(r => r.Process!.Code)
            .Take(200)
            .ToListAsync();

        ViewBag.RaciActivityRoles = await _context.ActivityRacis
            .Where(r => r.OrganizationUnitId == id)
            .Include(r => r.Activity)
                .ThenInclude(a => a!.Process)
            .OrderBy(r => r.Role).ThenBy(r => r.Activity!.Process!.Code)
            .Take(200)
            .ToListAsync();

        ViewBag.RaciTaskRoles = await _context.TaskRacis
            .Where(r => r.OrganizationUnitId == id)
            .Include(r => r.Task)
                .ThenInclude(t => t!.Activity)
                    .ThenInclude(a => a!.Process)
            .OrderBy(r => r.Role).ThenBy(r => r.Task!.Code)
            .Take(200)
            .ToListAsync();

        return View(unit);
    }

    /// <summary>
    /// Create organization unit form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.Create)]
    public async Task<IActionResult> Create(int? parentId = null)
    {
        await PopulateDropdowns();
        var model = new OrganizationUnit();
        if (parentId != null)
        {
            model.ParentId = parentId;
            var parent = await _context.OrganizationUnits.FindAsync(parentId.Value);
            if (parent != null)
                model.Level = parent.Level + 1;
        }
        return View(model);
    }

    /// <summary>
    /// Create organization unit
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrganizationUnit unit)
    {
        // F-004b: server-side validation. The OrganizationUnit model carries no
        // [Required]/uniqueness attributes but Code is unique-indexed, so a blank
        // or duplicate Code otherwise passes ModelState and throws an unhandled
        // DbUpdateException (500) at SaveChanges. Validate here and on the DB.
        if (string.IsNullOrWhiteSpace(unit.Code))
            ModelState.AddModelError(nameof(unit.Code), IsArabic ? "الرمز مطلوب." : "Code is required.");
        else if (await _context.OrganizationUnits.AnyAsync(u => u.Code == unit.Code))
            ModelState.AddModelError(nameof(unit.Code), (IsArabic ? "الرمز مستخدم بالفعل." : "Code already exists."));
        if (string.IsNullOrWhiteSpace(unit.NameEn))
            ModelState.AddModelError(nameof(unit.NameEn), IsArabic ? "الاسم بالإنجليزية مطلوب." : "Name (English) is required.");
        if (string.IsNullOrWhiteSpace(unit.NameAr))
            ModelState.AddModelError(nameof(unit.NameAr), IsArabic ? "الاسم بالعربية مطلوب." : "Name (Arabic) is required.");

        if (ModelState.IsValid)
        {
            // Id is a DB identity now — never assign client-side.
            unit.CreatedAt = DateTime.UtcNow;
            unit.UpdatedAt = DateTime.UtcNow;
            unit.CreatedById = CurrentUserId();

            // DisplayOrder is auto-assigned (form input is hidden) — next slot among siblings
            // (same ParentId), so each branch maintains its own ordering.
            if (unit.DisplayOrder <= 0)
            {
                var nextOrder = await _context.OrganizationUnits
                    .Where(u => u.ParentId == unit.ParentId)
                    .MaxAsync(u => (int?)u.DisplayOrder) ?? 0;
                unit.DisplayOrder = nextOrder + 1;
            }

            _context.OrganizationUnits.Add(unit);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Safety net for a race on the unique Code index.
                ModelState.AddModelError(nameof(unit.Code), (IsArabic ? "الرمز مستخدم بالفعل." : "Code already exists."));
                await PopulateDropdowns();
                return View(unit);
            }

            TempData["Success"] = _localizer["Success_OrganizationUnitCreated"].Value;
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns();
        return View(unit);
    }

    /// <summary>
    /// Edit organization unit form
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var unit = await _context.OrganizationUnits.FindAsync(id);
        if (unit == null || unit.IsDeleted)
            return NotFound();

        await PopulateDropdowns(id);
        return View(unit);
    }

    /// <summary>
    /// Edit organization unit
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OrganizationUnit unit)
    {
        if (id != unit.Id)
            return NotFound();

        // F-022: prevent an org-tree cycle — a unit must not be its own ancestor.
        if (unit.ParentId.HasValue)
        {
            if (unit.ParentId.Value == id)
            {
                ModelState.AddModelError(nameof(unit.ParentId),
                    IsArabic ? "لا يمكن أن تتبع الوحدة لنفسها." : "A unit cannot be its own parent.");
            }
            else
            {
                var ancestorId = unit.ParentId;
                var guard = 0;
                while (ancestorId.HasValue && guard++ < 50)
                {
                    if (ancestorId.Value == id)
                    {
                        ModelState.AddModelError(nameof(unit.ParentId),
                            IsArabic ? "لا يمكن اختيار وحدة فرعية كأصل (يُنشئ حلقة)." : "Cannot set a descendant as the parent (creates a cycle).");
                        break;
                    }
                    ancestorId = await _context.OrganizationUnits
                        .Where(u => u.Id == ancestorId.Value)
                        .Select(u => u.ParentId)
                        .FirstOrDefaultAsync();
                }
            }
        }

        if (ModelState.IsValid)
        {
            // FUNC-001: load-then-patch. A fully model-bound entity passed to
            // _context.Update() lets a crafted POST forge IsDeleted / CreatedById /
            // CreatedAt / Version / DeletedAt (all public-settable on the base).
            // Load the tracked row and copy only user-editable fields; the
            // system/audit fields and the auto-generated Code stay untouched.
            var existing = await _context.OrganizationUnits
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
            if (existing == null)
                return NotFound();

            existing.NameEn = unit.NameEn;
            existing.NameAr = unit.NameAr;
            existing.DescriptionEn = unit.DescriptionEn;
            existing.DescriptionAr = unit.DescriptionAr;
            existing.ParentId = unit.ParentId;
            existing.Level = unit.Level;
            existing.UnitType = unit.UnitType;
            existing.DisplayOrder = unit.DisplayOrder;
            existing.IsActive = unit.IsActive;
            existing.HeadUserId = unit.HeadUserId;
            existing.Email = unit.Email;
            existing.Phone = unit.Phone;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedById = CurrentUserId();
            existing.Version++;

            await _context.SaveChangesAsync();

            TempData["Success"] = _localizer["Success_OrganizationUnitUpdated"].Value;
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(id);
        return View(unit);
    }

    /// <summary>
    /// Delete organization unit (soft delete)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var unit = await _context.OrganizationUnits.FindAsync(id);
        if (unit == null)
            return NotFound();

        // FUNC-002: in-use guard. Soft-deleting a unit that still owns children
        // or work would orphan those rows (they'd point at a now-hidden FK
        // target and surface as dangling references on related dashboards).
        // Refuse if anything still references this unit. See CategoriesController.Delete.
        var childUnits = await _context.OrganizationUnits
            .CountAsync(u => u.ParentId == id && !u.IsDeleted);
        var processCount = await _context.Processes
            .CountAsync(p => p.OwningUnitId == id && !p.IsDeleted);
        var serviceCount = await _context.Services
            .CountAsync(s => s.OwningUnitId == id && !s.IsDeleted);
        var assetCount = await _context.Assets
            .CountAsync(a => a.AssignedToUnitId == id && !a.IsDeleted);
        var activityCount = await _context.Activities
            .CountAsync(a => a.OwningUnitId == id && !a.IsDeleted);
        var taskCount = await _context.ProcessTasks
            .CountAsync(t => t.OwningUnitId == id && !t.IsDeleted);
        var responsibilityCount = await _context.OrganizationUnitResponsibilities
            .CountAsync(r => r.OrganizationUnitId == id && !r.IsDeleted);

        var inUse = childUnits + processCount + serviceCount + assetCount
                  + activityCount + taskCount + responsibilityCount;
        if (inUse > 0)
        {
            // Inline bilingual message: no SharedResource key is added for this
            // (resx is owned elsewhere), so build the text from the current
            // culture and list every blocking reference count.
            var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            TempData["Error"] = isArabic
                ? $"لا يمكن الحذف: ما زالت هذه الوحدة مرتبطة بـ {childUnits} وحدات فرعية، {processCount} عمليات، {serviceCount} خدمات، {assetCount} أصول، {activityCount} أنشطة، {taskCount} مهام، {responsibilityCount} مسؤوليات. انقلها أو احذفها أولاً."
                : $"Cannot delete: this unit still has {childUnits} child units, {processCount} processes, {serviceCount} services, {assetCount} assets, {activityCount} activities, {taskCount} tasks, and {responsibilityCount} responsibilities referencing it. Move or delete them first.";
            return RedirectToAction(nameof(Index));
        }

        unit.IsDeleted = true;
        unit.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_OrganizationUnitDeleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Responsibilities (mandates) — child rows of OrganizationUnit, managed
    // inline from the unit's Details page. Add/Delete are inline POSTs from
    // the Responsibilities card; Edit opens a dedicated form for full edit
    // (description, active flag, display order).
    // ─────────────────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddResponsibility(int organizationUnitId, string nameEn, string nameAr, string? descriptionEn, string? descriptionAr)
    {
        var unit = await _context.OrganizationUnits
            .FirstOrDefaultAsync(u => u.Id == organizationUnitId && !u.IsDeleted);
        if (unit == null) return NotFound();

        if (string.IsNullOrWhiteSpace(nameEn) || nameEn.Trim().Length < 3 ||
            string.IsNullOrWhiteSpace(nameAr) || nameAr.Trim().Length < 3)
        {
            TempData["Error"] = _localizer["Error_ResponsibilityNameRequired"].Value;
            return RedirectToAction(nameof(Details), new { id = organizationUnitId });
        }

        var nextOrder = await _context.OrganizationUnitResponsibilities
            .Where(r => r.OrganizationUnitId == organizationUnitId && !r.IsDeleted)
            .MaxAsync(r => (int?)r.DisplayOrder) ?? 0;

        var responsibility = new OrganizationUnitResponsibility
        {
            Id = Guid.NewGuid().ToString(),
            OrganizationUnitId = organizationUnitId,
            NameEn = nameEn.Trim(),
            NameAr = nameAr.Trim(),
            DescriptionEn = string.IsNullOrWhiteSpace(descriptionEn) ? null : descriptionEn.Trim(),
            DescriptionAr = string.IsNullOrWhiteSpace(descriptionAr) ? null : descriptionAr.Trim(),
            DisplayOrder = nextOrder + 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedById = User.Identity?.Name
        };

        _context.OrganizationUnitResponsibilities.Add(responsibility);
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_ResponsibilityAdded"].Value;
        return RedirectToAction(nameof(Details), new { id = organizationUnitId });
    }

    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.Edit)]
    public async Task<IActionResult> EditResponsibility(string id)
    {
        var responsibility = await _context.OrganizationUnitResponsibilities
            .Include(r => r.OrganizationUnit)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        if (responsibility == null) return NotFound();

        return View(responsibility);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditResponsibility(string id, OrganizationUnitResponsibility responsibility)
    {
        if (id != responsibility.Id) return NotFound();

        var existing = await _context.OrganizationUnitResponsibilities
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        if (existing == null) return NotFound();

        if (!ModelState.IsValid)
        {
            responsibility.OrganizationUnit = await _context.OrganizationUnits.FindAsync(existing.OrganizationUnitId);
            return View(responsibility);
        }

        existing.NameEn = responsibility.NameEn.Trim();
        existing.NameAr = responsibility.NameAr.Trim();
        existing.DescriptionEn = string.IsNullOrWhiteSpace(responsibility.DescriptionEn) ? null : responsibility.DescriptionEn.Trim();
        existing.DescriptionAr = string.IsNullOrWhiteSpace(responsibility.DescriptionAr) ? null : responsibility.DescriptionAr.Trim();
        existing.DisplayOrder = responsibility.DisplayOrder;
        existing.IsActive = responsibility.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedById = User.Identity?.Name;
        existing.Version++;

        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_ResponsibilityUpdated"].Value;
        return RedirectToAction(nameof(Details), new { id = existing.OrganizationUnitId });
    }

    /// <summary>
    /// JSON typeahead endpoint for the reusable _ResponsibilityPicker partial.
    /// Strictly scoped to <paramref name="unitId"/> — only that unit's active
    /// responsibilities are returned. If <paramref name="unitId"/> is empty,
    /// returns an empty array so the picker prompts "select an organization
    /// unit first" instead of leaking cross-unit results.
    ///
    /// Authorize on OrganizationUnit.View — anyone who can see an org unit
    /// can search its mandates; the M2M write happens under the parent
    /// entity's own policy (Process.Edit, Service.Edit, etc.).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.View)]
    public async Task<IActionResult> SearchResponsibilities(string? q, string? unitId, int take = 20)
    {
        if (string.IsNullOrEmpty(unitId) || !int.TryParse(unitId, out var unitIdInt))
        {
            // No unit chosen yet — force the caller to pick one. Empty list
            // is cheaper than a "scoped: false" flag and keeps the JSON shape
            // identical for the client to render.
            return Json(Array.Empty<object>());
        }

        take = Math.Clamp(take, 1, 50);
        var needle = (q ?? string.Empty).Trim();

        var query = _context.OrganizationUnitResponsibilities
            .Where(r => !r.IsDeleted && r.IsActive && r.OrganizationUnitId == unitIdInt)
            .Include(r => r.OrganizationUnit)
            .AsQueryable();

        if (needle.Length > 0)
        {
            var like = $"%{needle}%";
            query = query.Where(r =>
                EF.Functions.Like(r.NameEn, like) ||
                EF.Functions.Like(r.NameAr, like));
        }

        var rows = await query
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.NameEn)
            .Take(take)
            .Select(r => new
            {
                id = r.Id,
                titleEn = r.NameEn,
                titleAr = r.NameAr,
                unitId = r.OrganizationUnitId,
                unitNameEn = r.OrganizationUnit == null ? "" : r.OrganizationUnit.NameEn,
                unitNameAr = r.OrganizationUnit == null ? "" : r.OrganizationUnit.NameAr
            })
            .ToListAsync();

        return Json(rows);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.Edit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResponsibility(string id)
    {
        var responsibility = await _context.OrganizationUnitResponsibilities
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        if (responsibility == null) return NotFound();

        var unitId = responsibility.OrganizationUnitId;
        responsibility.IsDeleted = true;
        responsibility.DeletedAt = DateTime.UtcNow;
        responsibility.UpdatedAt = DateTime.UtcNow;
        responsibility.UpdatedById = User.Identity?.Name;
        await _context.SaveChangesAsync();

        TempData["Success"] = _localizer["Success_ResponsibilityDeleted"].Value;
        return RedirectToAction(nameof(Details), new { id = unitId });
    }

    private async Task PopulateDropdowns(int? excludeId = null)
    {
        var query = _context.OrganizationUnits.Where(u => !u.IsDeleted && u.IsActive);
        if (excludeId != null)
            query = query.Where(u => u.Id != excludeId.Value);

        ViewBag.ParentUnits = new SelectList(await query.ToListAsync(), "Id", "Name");

        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
        var users = await _context.Users.ToListAsync();
        ViewBag.Users = new SelectList(
            users.Select(u => new { u.UserId, DisplayName = isArabic ? (u.EmployeeNameAr ?? u.EmployeeName) : u.EmployeeName }),
            "UserId", "DisplayName");
    }

    /// <summary>
    /// Current acting user's numeric id from the NameIdentifier claim, or null.
    /// OrganizationUnit.CreatedById/UpdatedById are int FKs to [user] now, so the
    /// audit stamp uses the user id (NameIdentifier) rather than the username
    /// (Identity.Name) that the string-id base entities still use.
    /// </summary>
    private int? CurrentUserId()
        => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid)
            ? uid
            : (int?)null;
}

