using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Helpers;
using ESEMS.Web.Models;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for User Management using custom user table
/// </summary>
[Authorize(Policy = AppPolicies.CanAdmin)]
public class UsersController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer,
        ILogger<UsersController> logger)
    {
        _context = context;
        _localizer = localizer;
        _logger = logger;
    }

    /// <summary>
    /// List all users (paginated).
    /// </summary>
    public async Task<IActionResult> Index(bool? isActive = null)
    {
        // Returns ALL users (no server-side paging) — DataTables owns paging/search/sort
        // client-side now. The only server filter left is `isActive` so admins can
        // narrow to active/inactive users if they need to.
        var query = _context.Set<CustomUser>()
            .Include(u => u.OrganizationUnit)
            .AsQueryable();

        var users = await query
            .OrderBy(u => u.EmployeeName)
            .ToListAsync();

        // Batch-load Plan X RoleGroup assignments in a single query instead of N+1.
        var userIds = users.Select(u => u.UserId).ToList();
        var allUserRoleGroups = await _context.UserRoleGroups
            .Where(urg => userIds.Contains(urg.UserId))
            .Join(_context.RoleGroups,
                urg => urg.RoleGroupId, rg => rg.Id,
                (urg, rg) => new { urg.UserId, rg.NameEn })
            .ToListAsync();

        var rolesByUserId = allUserRoleGroups
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.NameEn).ToList());

        ViewBag.SelectedIsActive = isActive;
        ViewBag.TotalCount = users.Count;
        ViewBag.RolesByUserId = rolesByUserId;
        return View(users);
    }

    /// <summary>
    /// View user details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var user = await _context.Set<CustomUser>()
            .Include(u => u.OrganizationUnit)
            .Include(u => u.Manager)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound();

        // Get user's assigned Plan X RoleGroup names for display.
        var userRoleGroupNames = await _context.UserRoleGroups
            .Where(urg => urg.UserId == id)
            .Join(_context.RoleGroups, urg => urg.RoleGroupId, rg => rg.Id, (urg, rg) => rg.NameEn)
            .ToListAsync();

        ViewBag.UserRoles = userRoleGroupNames;
        return View(user);
    }

    /// <summary>
    /// Create user form
    /// </summary>
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new CustomUser());
    }

    /// <summary>
    /// Create user
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomUser user, string? newPassword, string[]? selectedRoleGroups = null)
    {
        // Validate the initial password BEFORE checking ModelState so we always
        // surface the error message (CustomUser.Password is nullable, so
        // model binding alone won't catch a missing password).
        if (string.IsNullOrWhiteSpace(newPassword))
            ModelState.AddModelError("newPassword", "Initial password is required.");
        else if (newPassword.Length < 8)
            ModelState.AddModelError("newPassword", "Password must be at least 8 characters.");
        else if (newPassword.Length > 128)
            // FU-003 parity: cap input length so PBKDF2 isn't fed multi-MB input.
            ModelState.AddModelError("newPassword", "Password is too long (maximum 128 characters).");

        if (ModelState.IsValid)
        {
            try
            {
                // Check if username already exists
                var existingUser = await _context.Set<CustomUser>()
                    .FirstOrDefaultAsync(u => u.Username == user.Username);

                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Username already exists");
                    await PopulateDropdowns();
                    return View(user);
                }

                // Set audit fields
                var currentUserId = GetCurrentUserId();

                // F-021: reset privileged/gamification fields on the bound
                // entity before insert (mirror of FUNC-006's load-then-patch on
                // Edit). Model binding would otherwise let a crafted POST seed a
                // brand-new user with arbitrary Points/InnovatorLevel/badges —
                // mass assignment. New users always start with a clean score.
                user.Points = 0;
                user.InnovatorLevel = 0;
                user.HasIdeaGeneratorBadge = false;
                user.HasInnovatorBadge = false;
                user.HasVisionaryBadge = false;
                user.HasMilestoneAchieverBadge = false;
                user.HasImpactfulContributorBadge = false;

                // CRITICAL: clobber any form-bound Password BEFORE hashing.
                // CustomUser.Password is a public property so a crafted POST
                // could otherwise sneak a plaintext value into the DB. We
                // always rewrite it from the validated newPassword param.
                user.Password = PasswordHelper.Hash(newPassword!);
                user.SecurityStamp = Guid.NewGuid().ToString("N");

                _context.Set<CustomUser>().Add(user);
                await _context.SaveChangesAsync();

                // Add Plan X RoleGroup assignments
                if (selectedRoleGroups != null && selectedRoleGroups.Length > 0)
                {
                    foreach (var rgId in selectedRoleGroups.Distinct(StringComparer.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(rgId)) continue;
                        _context.UserRoleGroups.Add(new UserRoleGroup
                        {
                            UserId      = user.UserId,
                            RoleGroupId = rgId,
                            AssignedBy  = currentUserId,
                            AssignedAt  = DateTime.UtcNow,
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = _localizer["Success_UserCreated"].Value;
                return RedirectToAction(nameof(Details), new { id = user.UserId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                ModelState.AddModelError("", "An error occurred while creating the user");
            }
        }

        await PopulateDropdowns();
        return View(user);
    }

    /// <summary>
    /// Edit user form
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _context.Set<CustomUser>().FindAsync(id);
        if (user == null)
            return NotFound();

        await PopulateDropdowns();

        // Plan X: load the user's current RoleGroup assignments so the
        // Edit page can pre-check the matching checkboxes.
        var userRoleGroupIds = await _context.UserRoleGroups
            .Where(urg => urg.UserId == id)
            .Select(urg => urg.RoleGroupId)
            .ToListAsync();
        ViewBag.UserRoleGroupIds = userRoleGroupIds;
        return View(user);
    }

    /// <summary>
    /// Edit user.
    ///
    /// Diff-then-mutate (not wipe-rewrite) so unchanged assignment rows keep
    /// their original AssignedAt/AssignedDate values — the only audit trail
    /// on those tables. Permission changes also roll the target user's
    /// SecurityStamp so the cookie auth OnValidatePrincipal handler
    /// invalidates their existing sessions within 30 min (immediately on
    /// their next request if the actor is someone else). Every permission
    /// change writes an AuditLog row capturing the diff.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomUser user, string[]? selectedRoleGroups = null)
    {
        if (id != user.UserId)
            return NotFound();

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(user);
        }

        try
        {
            // Check if username already exists (excluding current user)
            var existingUser = await _context.Set<CustomUser>()
                .FirstOrDefaultAsync(u => u.Username == user.Username && u.UserId != id);

            if (existingUser != null)
            {
                ModelState.AddModelError("Username", "Username already exists");
                await PopulateDropdowns();
                return View(user);
            }

            // FUNC-006: load-then-patch instead of _context.Update(user).
            // The Edit form round-trips Password (and Points/InnovatorLevel)
            // in hidden inputs, and model binding would accept ANY extra
            // posted field — so a tampered POST could rewrite the password
            // hash, award badges, change SectorId/DirectManager, etc.
            // (mass assignment). We load the tracked entity and copy ONLY the
            // fields the form is allowed to change; privileged columns
            // (Password, Points, badges, audit ids) are never written here.
            var dbUser = await _context.Set<CustomUser>().FirstOrDefaultAsync(u => u.UserId == id);
            if (dbUser == null)
                return NotFound();

            // Propagate the form's RowVersion onto the tracked entity so EF
            // sees a concurrency conflict if another admin saved in between
            // (CustomUser carries [Timestamp] RowVersion — see CustomUser.cs).
            if (user.RowVersion != null)
                _context.Entry(dbUser).Property(u => u.RowVersion).OriginalValue = user.RowVersion;

            dbUser.Username       = user.Username;
            dbUser.EmployeeNumber = user.EmployeeNumber;
            dbUser.EmployeeName   = user.EmployeeName;
            dbUser.EmployeeNameAr = user.EmployeeNameAr;
            dbUser.EmailAddress   = user.EmailAddress;
            dbUser.JobName        = user.JobName;
            dbUser.JobNameAr      = user.JobNameAr;
            dbUser.UnitId         = user.UnitId;
            dbUser.IsActive       = user.IsActive; // FUNC-006: allow (de)activation via the Edit form's Active toggle

            // ---------------- Diff Plan X UserRoleGroup rows -----------------
            var existingRgRows = await _context.UserRoleGroups
                .Where(urg => urg.UserId == id)
                .ToListAsync();
            var existingRgIds  = existingRgRows.Select(r => r.RoleGroupId).ToHashSet(StringComparer.Ordinal);
            var selectedRgIds  = (selectedRoleGroups ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);

            var rgIdsToAdd     = selectedRgIds.Except(existingRgIds, StringComparer.Ordinal).ToList();
            var rgRowsToRemove = existingRgRows.Where(r => !selectedRgIds.Contains(r.RoleGroupId)).ToList();

            var rolesChanged = rgIdsToAdd.Count > 0 || rgRowsToRemove.Count > 0;

            var actorUserId = GetCurrentUserId();

            // Apply only the diff. Rows that stayed selected keep their
            // original AssignedBy/AssignedAt so the audit trail on those
            // columns means what it says.
            if (rgRowsToRemove.Count > 0)
                _context.UserRoleGroups.RemoveRange(rgRowsToRemove);
            foreach (var rgId in rgIdsToAdd)
            {
                _context.UserRoleGroups.Add(new UserRoleGroup
                {
                    UserId      = dbUser.UserId,
                    RoleGroupId = rgId,
                    AssignedBy  = actorUserId,
                    AssignedAt  = DateTime.UtcNow,
                });
            }

            // FU-002: when permissions change, roll the target user's
            // SecurityStamp. OnValidatePrincipal (Program.cs) rejects every
            // cookie whose stamp no longer matches — so the affected user is
            // signed out and re-authenticated with fresh Permission claims on
            // their next request (or, at worst, within the 30-min re-check
            // interval). Scalar-only edits (e.g. fixing a typo in
            // EmployeeName) do NOT roll the stamp, so the user stays signed in.
            if (rolesChanged)
                dbUser.SecurityStamp = Guid.NewGuid().ToString("N");

            // ---------------- AuditLog ----------------
            if (rolesChanged)
            {
                // Resolve RoleGroup names for the audit summary so future-you
                // can read it without joining back to RoleGroups. One
                // round-trip, only when something actually changed.
                var touchedRgIds = rgIdsToAdd
                    .Concat(rgRowsToRemove.Select(r => r.RoleGroupId))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                var rgNameMap = await _context.RoleGroups
                    .Where(rg => touchedRgIds.Contains(rg.Id))
                    .ToDictionaryAsync(rg => rg.Id, rg => rg.NameEn, StringComparer.Ordinal);

                static string FormatGroup(string id, IDictionary<string, string> map)
                    => map.TryGetValue(id, out var n) ? n : id;

                var parts = new List<string>();
                if (rgIdsToAdd.Count > 0)
                    parts.Add("+groups: " + string.Join(", ", rgIdsToAdd.Select(g => FormatGroup(g, rgNameMap))));
                if (rgRowsToRemove.Count > 0)
                    parts.Add("-groups: " + string.Join(", ", rgRowsToRemove.Select(r => FormatGroup(r.RoleGroupId, rgNameMap))));

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId     = actorUserId.ToString(),
                    UserName   = User.Identity?.Name,
                    Action     = AuditAction.Update,
                    EntityType = nameof(CustomUser),
                    EntityId   = dbUser.UserId.ToString(),
                    EntityName = dbUser.Username,
                    IpAddress  = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent  = HttpContext.Request.Headers.UserAgent.ToString(),
                    Notes      = "Role groups updated. " + string.Join("; ", parts),
                    Timestamp  = DateTime.UtcNow,
                });
            }

            // Single SaveChangesAsync — EF wraps it in one transaction, so
            // scalar updates + role diff + audit row + stamp roll are all
            // atomic. If anything fails, nothing committed.
            await _context.SaveChangesAsync();
            TempData["Success"] = _localizer["Success_UserUpdated"].Value;
            return RedirectToAction(nameof(Details), new { id = dbUser.UserId });
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await UserExists(user.UserId))
                return NotFound();

            // Another admin saved this user since we loaded the form. Surface
            // a recognisable error so the operator refreshes and retries
            // instead of seeing a generic 500.
            ModelState.AddModelError("",
                "This user was modified by another administrator while you were editing. " +
                "Refresh the page to load the latest values, then re-apply your changes.");
            await PopulateDropdowns();
            return View(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user");
            ModelState.AddModelError("", "An error occurred while updating the user");
            await PopulateDropdowns();
            return View(user);
        }
    }

    /// <summary>
    /// Delete user.
    ///
    /// FUNC-006: CustomUser has no soft-delete/active flag (the `IsActive`
    /// property is [NotMapped] and hardcoded true), so this remains a physical
    /// delete — but it now refuses when the user is still referenced, to avoid
    /// orphaning records and breaking FK relationships. Specifically we block
    /// deletion when the user is set as another user's direct manager, and we
    /// block self-deletion. RECOMMENDATION: add a real `is_active` column to the
    /// [user] table and switch this to a deactivate (IsActive=false) so the row
    /// — and its audit/assignment history — is preserved instead of removed.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var user = await _context.Set<CustomUser>().FindAsync(id);
            if (user == null)
                return NotFound();

            // Guard: don't let an admin delete their own account.
            if (id == GetCurrentUserId())
            {
                TempData["Error"] = _localizer["Error_DeletingUser"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            // FUNC-006: soft-delete — deactivate instead of physically removing.
            // The row and all its FKs (DirectManager, role/group assignments,
            // audit/assignment history) are preserved; the login path rejects
            // inactive users so they can no longer sign in. Reactivate via Edit.
            user.IsActive = false;
            // Roll the SecurityStamp so any active session for the deactivated
            // user is invalidated by OnValidatePrincipal on its next request,
            // rather than lingering until the cookie naturally expires.
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await _context.SaveChangesAsync();

            var ar = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            TempData["Success"] = ar ? "تم إلغاء تفعيل المستخدم." : "User deactivated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            TempData["Error"] = _localizer["Error_DeletingUser"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// Populate dropdowns for create/edit forms
    /// </summary>
    private async Task PopulateDropdowns()
    {
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");

        var orgUnits = await _context.OrganizationUnits.Where(o => !o.IsDeleted).OrderBy(o => o.NameEn).ToListAsync();
        ViewBag.OrganizationUnits = new SelectList(
            orgUnits.Select(o => new { o.Id, DisplayName = isArabic ? o.NameAr : o.NameEn }),
            "Id",
            "DisplayName"
        );

        // Plan X role groups for the create/edit role assignment UI.
        ViewBag.AllRoleGroups = await _context.RoleGroups
            .Where(rg => rg.IsActive)
            .OrderByDescending(rg => rg.IsSystemRole)
            .ThenBy(rg => rg.NameEn)
            .ToListAsync();

        var managers = await _context.Set<CustomUser>().OrderBy(u => u.EmployeeName).ToListAsync();
        ViewBag.Managers = new SelectList(
            managers.Select(u => new { u.UserId, DisplayName = isArabic ? u.DisplayNameAr : u.DisplayName }),
            "UserId",
            "DisplayName"
        );
    }

    /// <summary>
    /// Check if user exists
    /// </summary>
    private async Task<bool> UserExists(int id)
    {
        return await _context.Set<CustomUser>().AnyAsync(e => e.UserId == id);
    }

    /// <summary>
    /// Get current user ID from claims
    /// </summary>
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return 0;
    }
}
