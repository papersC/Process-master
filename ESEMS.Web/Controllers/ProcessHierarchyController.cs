using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for Process Hierarchy Visualization + inline editing.
/// </summary>
[Authorize]
public class ProcessHierarchyController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<ProcessHierarchyController> _logger;

    public ProcessHierarchyController(
        ApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer,
        ILogger<ProcessHierarchyController> logger)
    {
        _context = context;
        _localizer = localizer;
        _logger = logger;
    }

    /// <summary>
    /// Display process hierarchy organized by organization structure
    /// </summary>
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.View)]
    public async Task<IActionResult> Index(string? search = null, string? departmentFilter = null, string? statusFilter = null)
    {
        try
        {
            // Get all organization units with their processes
            // Load Sectors (Level 0) with nested Children (Departments and Sections)
            var query = _context.OrganizationUnits
                .Include(o => o.Children) // Departments (Level 1)
                    .ThenInclude(d => d.Children) // Sections (Level 2)
                        .ThenInclude(s => s.OwnedProcesses) // Processes under Sections
                            .ThenInclude(p => p.ProcessGroup)
                .Include(o => o.Children) // Departments (Level 1)
                    .ThenInclude(d => d.OwnedProcesses) // Processes under Departments
                        .ThenInclude(p => p.ProcessGroup)
                .Include(o => o.OwnedProcesses) // Processes under Sectors
                    .ThenInclude(p => p.ProcessGroup)
                .Where(o => o.IsActive)
                .AsQueryable();

            // Apply department filter if specified
            if (!string.IsNullOrWhiteSpace(departmentFilter) && int.TryParse(departmentFilter, out var departmentFilterId))
            {
                query = query.Where(o => o.Id == departmentFilterId || o.ParentId == departmentFilterId);
            }

            // Get all levels: Sectors (0), Departments (1), Sections (2)
            var organizationUnits = await query
                .Where(o => o.Level <= 2)
                .OrderBy(o => o.Level)
                .ThenBy(o => o.DisplayOrder)
                .ToListAsync();

            // Apply search filter if specified
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                organizationUnits = organizationUnits
                    .Where(o =>
                        o.NameEn.ToLower().Contains(searchLower) ||
                        o.NameAr.Contains(search) ||
                        o.Code.ToLower().Contains(searchLower) ||
                        o.Children.Any(c =>
                            c.NameEn.ToLower().Contains(searchLower) ||
                            c.NameAr.Contains(search) ||
                            c.OwnedProcesses.Any(p =>
                                p.NameEn.ToLower().Contains(searchLower) ||
                                p.NameAr.Contains(search) ||
                                p.Code.ToLower().Contains(searchLower))))
                    .ToList();
            }

            // Service counts by owning unit (used by the hierarchy view to show Service totals per unit)
            // We intentionally keep this as a grouped query (no heavy Includes) and pass a dictionary to the view.
            var unitIds = new HashSet<int>();
            void CollectIds(OrganizationUnit u)
            {
                if (u == null) return;
                unitIds.Add(u.Id);
                if (u.Children == null) return;
                foreach (var c in u.Children) CollectIds(c);
            }

            foreach (var u in organizationUnits) CollectIds(u);

            // The view keys ServiceCountsByUnit by the unit id string, so project
            // the grouped int counts into a string-keyed dictionary.
            var serviceCountsRaw = await _context.Services
                .Where(s => s.OwningUnitId != null && unitIds.Contains(s.OwningUnitId.Value))
                .GroupBy(s => s.OwningUnitId!.Value)
                .Select(g => new { UnitId = g.Key, Count = g.Count() })
                .ToListAsync();
            var serviceCountsByUnit = serviceCountsRaw
                .ToDictionary(x => x.UnitId.ToString(), x => x.Count);

            ViewBag.ServiceCountsByUnit = serviceCountsByUnit;

            // Get all departments for filter dropdown
            var departments = await _context.OrganizationUnits
                .Where(o => o.Level == 1 && o.IsActive)
                .OrderBy(o => o.DisplayOrder)
                .ToListAsync();

            ViewBag.Departments = departments;
            ViewBag.Search = search;
            ViewBag.DepartmentFilter = departmentFilter;
            ViewBag.StatusFilter = statusFilter;

            return View(organizationUnits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading process hierarchy");
            TempData["Error"] = _localizer["Error_LoadingData"].Value;
            return View(new List<OrganizationUnit>());
        }
    }

    /// <summary>
    /// Get organization unit details with processes (AJAX)
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.Module.OrganizationUnit.View)]
    public async Task<IActionResult> GetUnitDetails(string id)
    {
        try
        {
            if (!int.TryParse(id, out var unitId))
                return NotFound(new { success = false, message = "Organization unit not found" });

            var unit = await _context.OrganizationUnits
                .Include(o => o.Children)
                    .ThenInclude(c => c.OwnedProcesses)
                        .ThenInclude(p => p.ProcessGroup)
                .Include(o => o.OwnedProcesses)
                    .ThenInclude(p => p.ProcessGroup)
                .FirstOrDefaultAsync(o => o.Id == unitId);

            if (unit == null)
                return NotFound(new { success = false, message = "Organization unit not found" });

            var isRtl = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;

            // Avoid N+1 queries: get service counts for all direct children in one grouped query
            var childIds = unit.Children?.Select(c => c.Id).ToList() ?? new List<int>();
            var childServiceCounts = childIds.Count == 0
                ? new Dictionary<int, int>()
                : await _context.Services
                    .Where(s => s.OwningUnitId != null && childIds.Contains(s.OwningUnitId.Value))
                    .GroupBy(s => s.OwningUnitId!.Value)
                    .Select(g => new { UnitId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.UnitId, x => x.Count);

            var result = new
            {
                success = true,
                unit = new
                {
                    id = unit.Id,
                    name = isRtl ? unit.NameAr : unit.NameEn,
                    nameEn = unit.NameEn,
                    nameAr = unit.NameAr,
                    code = unit.Code,
                    level = unit.Level,
                    parentId = unit.ParentId,
                    displayOrder = unit.DisplayOrder,
                    email = unit.Email,
                    phone = unit.Phone,
                    isActive = unit.IsActive,
                    children = unit.Children.Select(c => new
                    {
                        id = c.Id,
                        name = isRtl ? c.NameAr : c.NameEn,
                        code = c.Code,
                        processCount = c.OwnedProcesses.Count,
                        serviceCount = childServiceCounts.TryGetValue(c.Id, out var sc) ? sc : 0
                    }).ToList(),
                    processes = unit.OwnedProcesses.Select(p => new
                    {
                        id = p.Id,
                        name = isRtl ? p.NameAr : p.NameEn,
                        code = p.Code,
                        processGroup = isRtl ? p.ProcessGroup?.NameAr : p.ProcessGroup?.NameEn,
                        status = p.Status.ToString()
                    }).ToList()
                }
            };

            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading organization unit details");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }
}

