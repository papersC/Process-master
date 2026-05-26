using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Role Groups — named bundles of (module, action) permissions with optional
/// scope (All / OwningUnit / Process). Inspired by PManagement's permission
/// matrix UX: rows = modules, columns = actions, with row/column select-all
/// and system-role protection for seeded groups.
/// </summary>
[Authorize(Policy = AppPolicies.CanAdmin)]
public class RoleGroupsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RoleGroupsController> _logger;

    /// <summary>
    /// Catalog of modules that appear as rows in the permission matrix, plus
    /// the set of actions each module legitimately supports. Actions not in
    /// a module's whitelist render as empty cells so admins can't grant
    /// nonsensical permissions (e.g. Approve on Reports).
    /// </summary>
    public static readonly (string Key, string LabelEn, string LabelAr, string[] ValidActions)[] ModuleCatalog = new[]
    {
        ("Improvement",      "Improvement Initiatives", "مبادرات التحسين",
            new[] { "View", "Create", "Edit", "Delete", "Approve", "Export" }),
        ("Measurement",      "Measurements & Readings", "المقاييس والقراءات",
            new[] { "View", "Create", "Edit", "Delete", "Export" }),
        ("Process",          "Processes", "العمليات",
            new[] { "View", "Create", "Edit", "Delete", "Export" }),
        ("Service",          "Service Catalog", "كتالوج الخدمات",
            new[] { "View", "Create", "Edit", "Delete", "Export" }),
        ("Risk",             "Enterprise Risks", "مخاطر المؤسسة",
            new[] { "View", "Create", "Edit", "Delete", "Approve", "Export" }),
        ("Asset",            "Assets & Maintenance", "الأصول والصيانة",
            new[] { "View", "Create", "Edit", "Delete", "Export" }),
        ("Incident",         "Incidents", "الحوادث",
            new[] { "View", "Create", "Edit", "Delete" }),
        ("Problem",          "Problems", "المشكلات",
            new[] { "View", "Create", "Edit", "Delete" }),
        ("ChangeRequest",    "Change Requests", "طلبات التغيير",
            new[] { "View", "Create", "Edit", "Delete", "Approve" }),
        ("WorkflowTask",     "Tasks", "المهام",
            new[] { "View", "Create", "Edit", "Delete" }),
        ("Workflow",         "Approvals Inbox", "صندوق الموافقات",
            new[] { "View", "Approve" }),
        ("Workload",         "Workload Analysis", "تحليل عبء العمل",
            new[] { "View", "Create", "Edit", "Delete", "Export" }),
        ("OrganizationUnit", "Organization Units", "الوحدات التنظيمية",
            new[] { "View", "Create", "Edit", "Delete" }),
        ("Reports",          "Reports", "التقارير",
            new[] { "View", "Export" }),
        ("Users",            "User Management", "إدارة المستخدمين",
            new[] { "View", "Create", "Edit", "Delete" }),
        ("Settings",         "Settings Hub", "مركز الإعدادات",
            new[] { "View", "Edit" }),
        ("Ai",               "AI Assistant", "المساعد الذكي",
            new[] { "View" }),
    };

    public static readonly string[] AllActions = new[] { "View", "Create", "Edit", "Delete", "Approve", "Export" };

    public RoleGroupsController(ApplicationDbContext context, ILogger<RoleGroupsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Standalone Role Groups page is deprecated — everything lives inside
    /// the Settings Hub tab now. Redirect any old bookmarks.
    /// </summary>
    public IActionResult Index()
    {
        return RedirectToAction("Index", "SettingsHub", new { tab = "roles" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromForm] SaveRoleGroupRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NameEn))
            return Json(new { success = false, error = "Name (EN) is required." });

        try
        {
            RoleGroup g;
            bool isNew = false;
            if (!string.IsNullOrWhiteSpace(req.Id))
            {
                g = await _context.RoleGroups.FirstOrDefaultAsync(x => x.Id == req.Id)
                    ?? throw new InvalidOperationException("Role group not found.");
            }
            else
            {
                g = new RoleGroup();
                _context.RoleGroups.Add(g);
                isNew = true;
            }

            // System roles cannot be renamed or deleted via the UI — permissions
            // can still be edited.
            if (!g.IsSystemRole || isNew)
            {
                g.NameEn = req.NameEn.Trim();
                g.NameAr = (req.NameAr ?? req.NameEn).Trim();
                g.Code = string.IsNullOrWhiteSpace(req.Code) ? Slugify(req.NameEn) : req.Code.Trim();
            }

            g.DescriptionEn = req.DescriptionEn;
            g.DescriptionAr = req.DescriptionAr;
            g.ScopeLevel = string.IsNullOrWhiteSpace(req.ScopeLevel) ? "All" : req.ScopeLevel;

            // Validate the requested permission keys against the AppPolicies
            // catalog. Earlier behavior saved req.Permissions verbatim, so a
            // typo like "Improvment.View" (missing 'e') would land in the DB
            // unnoticed and silently grant nothing at login. Accepted forms:
            //   - explicit Module.Action keys from AppPolicies.AllModuleActions
            //   - per-module wildcard "Module.*" for every module in the catalog
            //   - the global wildcard "*.*"
            // After validation we normalize: trim, drop blanks, dedupe by
            // string, then SEMANTICALLY collapse redundant entries:
            //   - if "*.*" is granted, drop every other key (all redundant)
            //   - if "Module.*" is granted, drop every other "Module.X" key
            // Finally sort alphabetically so the stored CSV is canonical.
            var allowed = new HashSet<string>(AppPolicies.AllModuleActions, StringComparer.Ordinal) { "*.*" };
            foreach (var m in ModuleCatalog)
                allowed.Add($"{m.Key}.*");

            var requested = (req.Permissions ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var invalid = requested.Where(p => !allowed.Contains(p)).ToArray();
            if (invalid.Length > 0)
                return Json(new
                {
                    success = false,
                    error = $"Unknown permission keys: {string.Join(", ", invalid)}"
                });

            var collapsed = CollapseRedundantPermissions(requested);
            g.Permissions = string.Join(",", collapsed.OrderBy(p => p, StringComparer.Ordinal));
            g.Icon = string.IsNullOrWhiteSpace(req.Icon) ? "users" : req.Icon;
            g.Color = string.IsNullOrWhiteSpace(req.Color) ? "#005B99" : req.Color;
            g.IsActive = true;
            g.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Json(new { success = true, id = g.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save role group");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var g = await _context.RoleGroups.FirstOrDefaultAsync(x => x.Id == id);
            if (g == null) return Json(new { success = true });
            if (g.IsSystemRole)
                return Json(new { success = false, error = "System roles cannot be deleted." });

            _context.RoleGroups.Remove(g);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Duplicate an existing group (useful when starting from a template).
    /// Copies permissions + scope, clears system-role flag, suffixes name with "(Copy)".
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(string id)
    {
        try
        {
            var src = await _context.RoleGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (src == null) return Json(new { success = false, error = "Source not found." });

            var copy = new RoleGroup
            {
                NameEn = src.NameEn + " (Copy)",
                NameAr = src.NameAr + " (نسخة)",
                Code = Slugify(src.NameEn + "-copy"),
                DescriptionEn = src.DescriptionEn,
                DescriptionAr = src.DescriptionAr,
                ScopeLevel = src.ScopeLevel,
                Permissions = src.Permissions,
                Icon = src.Icon,
                Color = src.Color,
                IsActive = true,
                IsSystemRole = false
            };
            _context.RoleGroups.Add(copy);
            await _context.SaveChangesAsync();
            return Json(new { success = true, id = copy.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Semantically dedupe a validated permission set:
    ///   - "*.*"      ⇒ drop every other key (all redundant under the wildcard)
    ///   - "Module.*" ⇒ drop every other "Module.X" key for that Module
    /// Pure function — call after string-validation, before joining for storage.
    /// </summary>
    internal static IReadOnlyCollection<string> CollapseRedundantPermissions(IEnumerable<string> permissions)
    {
        var set = new HashSet<string>(permissions, StringComparer.Ordinal);
        if (set.Contains("*.*"))
            return new[] { "*.*" };

        // Find every "Module.*" wildcard and drop the matching exact entries.
        var moduleWildcards = set
            .Where(p => p.EndsWith(".*", StringComparison.Ordinal))
            .Select(p => p[..^2]) // strip the trailing ".*"
            .ToList();

        if (moduleWildcards.Count == 0)
            return set;

        var result = new HashSet<string>(set, StringComparer.Ordinal);
        foreach (var p in set)
        {
            // Skip the wildcards themselves; they stay.
            if (p.EndsWith(".*", StringComparison.Ordinal)) continue;

            var dot = p.IndexOf('.');
            if (dot <= 0) continue;
            var module = p[..dot];
            if (moduleWildcards.Contains(module, StringComparer.Ordinal))
                result.Remove(p);
        }
        return result;
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var lower = input.ToLowerInvariant().Trim();
        var cleaned = System.Text.RegularExpressions.Regex.Replace(lower, "[^a-z0-9\\s-]", "");
        var dashed = System.Text.RegularExpressions.Regex.Replace(cleaned, "[\\s-]+", "-");
        return dashed.Trim('-');
    }

    public class SaveRoleGroupRequest
    {
        public string? Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? Code { get; set; }
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? ScopeLevel { get; set; }
        public string? Permissions { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
    }
}
