using System.Security.Claims;
using System.Text.Json;
using ESEMS.Web;
using ESEMS.Web.Controllers;
using ESEMS.Web.Data;
using ESEMS.Web.Models;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Tests.TestFixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Direct controller unit tests for the user/role-management surface reworked
/// in the 2026-05-26 RBAC cleanup. Bypasses WebApplicationFactory (the
/// shared test factory inherits Negotiate from Program.cs which TestServer
/// can't initialise) and exercises the controller methods directly against an
/// InMemory <see cref="ApplicationDbContext"/>.
///
/// Five things this suite pins:
///   1. Edit POST that changes role groups rolls the target user's SecurityStamp.
///   2. Edit POST that only touches scalar fields does NOT roll the stamp.
///   3. Edit POST writes an AuditLog row capturing the group-change diff.
///   4. RoleGroups/Save rejects unknown permission keys (typo defense).
///   5. RoleGroups/Save accepts the module-level and global wildcards.
/// </summary>
public class RoleManagementTests
{
    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private static UsersController NewUsersController(ApplicationDbContext db, int actorUserId = 1)
    {
        var localizer = new Mock<IStringLocalizer<SharedResource>>();
        // Localizer indexer returns a LocalizedString with the key as both
        // name and value — enough for `_localizer["..."].Value` calls in
        // controller code to return something printable.
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var c = new UsersController(db, localizer.Object, NullLogger<UsersController>.Instance);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString()),
                new Claim(ClaimTypes.Name, $"admin-{actorUserId}"),
            }, "TestAuth")),
        };
        c.ControllerContext = new ControllerContext { HttpContext = http };
        c.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        c.Url = Mock.Of<IUrlHelper>();
        return c;
    }

    private static RoleGroupsController NewRoleGroupsController(ApplicationDbContext db)
    {
        var c = new RoleGroupsController(db, NullLogger<RoleGroupsController>.Instance);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "admin"),
            }, "TestAuth")),
        };
        c.ControllerContext = new ControllerContext { HttpContext = http };
        return c;
    }

    /// <summary>
    /// Seed: a CustomUser plus two RoleGroups so tests can pivot the user
    /// from one assignment to another. Returns (user, fromGroup, toGroup).
    /// </summary>
    private static async Task<(CustomUser user, RoleGroup fromGroup, RoleGroup toGroup, string initialStamp)>
        SeedUserAndTwoGroupsAsync(ApplicationDbContext db)
    {
        var initialStamp = Guid.NewGuid().ToString("N");
        var user = new CustomUser
        {
            UserId        = 100,
            Username      = "rm-user",
            EmployeeName  = "RoleMgmt User",
            EmailAddress  = "rm@example.test",
            IsActive      = true,
            SecurityStamp = initialStamp,
        };
        db.CustomUsers.Add(user);

        var fromGroup = new RoleGroup
        {
            Id          = "from-group-id",
            Code        = "from",
            NameEn      = "From Group",
            NameAr      = "من",
            ScopeLevel  = "All",
            Permissions = "Improvement.View",
            IsActive    = true,
        };
        var toGroup = new RoleGroup
        {
            Id          = "to-group-id",
            Code        = "to",
            NameEn      = "To Group",
            NameAr      = "إلى",
            ScopeLevel  = "All",
            Permissions = "Process.View",
            IsActive    = true,
        };
        db.RoleGroups.AddRange(fromGroup, toGroup);

        db.UserRoleGroups.Add(new UserRoleGroup
        {
            UserId      = 100,
            RoleGroupId = fromGroup.Id,
            AssignedBy  = 1,
            AssignedAt  = DateTime.UtcNow.AddDays(-7),
        });
        await db.SaveChangesAsync();
        return (user, fromGroup, toGroup, initialStamp);
    }

    /// <summary>Posted form model mirroring what Edit.cshtml submits.</summary>
    private static CustomUser FormUser(int id, string username, string employeeName) =>
        new()
        {
            UserId       = id,
            Username     = username,
            EmployeeName = employeeName,
            IsActive     = true,
        };

    // ────────────────────────────────────────────────────────────────────────
    // 1. Role-group change rolls SecurityStamp
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_RoleGroupChange_RollsSecurityStamp()
    {
        using var db = TestDbContextFactory.Create();
        var (_, _, toGroup, initialStamp) = await SeedUserAndTwoGroupsAsync(db);
        var controller = NewUsersController(db);

        // Posted form: still user 100, same username, but a DIFFERENT
        // RoleGroup selected (was 'from-group-id', now 'to-group-id').
        var form = FormUser(100, "rm-user", "RoleMgmt User");
        var result = await controller.Edit(100, form,
            selectedRoleGroups: new[] { toGroup.Id });

        Assert.IsType<RedirectToActionResult>(result);

        var after = await db.CustomUsers.AsNoTracking().FirstAsync(u => u.UserId == 100);
        Assert.NotEqual(initialStamp, after.SecurityStamp);
        Assert.False(string.IsNullOrWhiteSpace(after.SecurityStamp));
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. Scalar-only edit does NOT roll SecurityStamp
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_ScalarOnly_DoesNotRollSecurityStamp()
    {
        using var db = TestDbContextFactory.Create();
        var (_, fromGroup, _, initialStamp) = await SeedUserAndTwoGroupsAsync(db);
        var controller = NewUsersController(db);

        // Same RoleGroup assignment as before — only EmployeeName differs.
        var form = FormUser(100, "rm-user", "Fixed Typo Name");
        var result = await controller.Edit(100, form,
            selectedRoleGroups: new[] { fromGroup.Id });

        Assert.IsType<RedirectToActionResult>(result);

        var after = await db.CustomUsers.AsNoTracking().FirstAsync(u => u.UserId == 100);
        Assert.Equal(initialStamp, after.SecurityStamp);
        Assert.Equal("Fixed Typo Name", after.EmployeeName);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. Edit POST writes an AuditLog row capturing the role diff
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_RoleGroupChange_WritesAuditLog()
    {
        using var db = TestDbContextFactory.Create();
        var (_, _, toGroup, _) = await SeedUserAndTwoGroupsAsync(db);
        var controller = NewUsersController(db, actorUserId: 42);

        var form = FormUser(100, "rm-user", "RoleMgmt User");
        await controller.Edit(100, form, selectedRoleGroups: new[] { toGroup.Id });

        var audit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == nameof(CustomUser)
                     && a.EntityId == "100"
                     && a.Action == AuditAction.Update)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("42", audit!.UserId); // stamped from the controller's User claim
        Assert.NotNull(audit.Notes);
        Assert.Contains("groups", audit.Notes!, StringComparison.OrdinalIgnoreCase);
        // The diff text should mention BOTH the removed (From Group) and the
        // added (To Group) group names so an auditor can read the row.
        Assert.Contains("From Group", audit.Notes!);
        Assert.Contains("To Group", audit.Notes!);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Bonus: scalar-only edit writes NO AuditLog row (the audit gate is
    // wired to actual role/group diff, not every save).
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_ScalarOnly_WritesNoAuditLog()
    {
        using var db = TestDbContextFactory.Create();
        var (_, fromGroup, _, _) = await SeedUserAndTwoGroupsAsync(db);
        var controller = NewUsersController(db);

        var form = FormUser(100, "rm-user", "Fixed Typo Name");
        await controller.Edit(100, form, selectedRoleGroups: new[] { fromGroup.Id });

        var anyAudit = await db.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityType == nameof(CustomUser) && a.EntityId == "100");
        Assert.False(anyAudit, "Scalar-only edits should not log a role change");
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. RoleGroups/Save rejects an unknown permission key
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoleGroupSave_RejectsUnknownPermission()
    {
        using var db = TestDbContextFactory.Create();
        var controller = NewRoleGroupsController(db);

        var req = new RoleGroupsController.SaveRoleGroupRequest
        {
            NameEn      = "Test Bad",
            Permissions = "Improvment.View,Process.View",  // typo on Improvement
        };

        var result = await controller.Save(req);
        var json = Assert.IsType<JsonResult>(result);
        var dict = ToDict(json.Value);

        Assert.False((bool)dict["success"]!);
        Assert.Contains("Improvment.View", (string)dict["error"]!);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. RoleGroups/Save accepts module wildcards and the global wildcard
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoleGroupSave_AcceptsWildcards()
    {
        using var db = TestDbContextFactory.Create();
        var controller = NewRoleGroupsController(db);

        var req = new RoleGroupsController.SaveRoleGroupRequest
        {
            NameEn      = "Test Good",
            Permissions = "Improvement.Edit,Process.View,Risk.Approve",
        };

        var result = await controller.Save(req);
        var json = Assert.IsType<JsonResult>(result);
        var dict = ToDict(json.Value);

        Assert.True((bool)dict["success"]!,
            "Explicit Module.Action keys should pass validation");
        Assert.NotNull(dict["id"]);

        // Round-trip: the saved RoleGroup should normalise to a stable
        // sorted canonical CSV (alphabetical) — pin this so a future change
        // to the normaliser doesn't silently regress the storage format.
        var saved = await db.RoleGroups.AsNoTracking()
            .FirstAsync(g => g.NameEn == "Test Good");
        Assert.Equal("Improvement.Edit,Process.View,Risk.Approve", saved.Permissions);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5b. Semantic dedupe — *.* absorbs everything; Module.* absorbs Module.X
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoleGroupSave_GlobalWildcard_CollapsesEverythingElse()
    {
        using var db = TestDbContextFactory.Create();
        var controller = NewRoleGroupsController(db);

        var req = new RoleGroupsController.SaveRoleGroupRequest
        {
            NameEn      = "GlobalWildcard",
            Permissions = "*.*,Process.*,Improvement.Edit,Risk.Approve",
        };
        var result = await controller.Save(req);
        Assert.True((bool)ToDict(((JsonResult)result).Value)["success"]!);

        var saved = await db.RoleGroups.AsNoTracking()
            .FirstAsync(g => g.NameEn == "GlobalWildcard");
        // *.* makes every other key redundant — only *.* should survive.
        Assert.Equal("*.*", saved.Permissions);
    }

    [Fact]
    public async Task RoleGroupSave_ModuleWildcard_CollapsesModuleSpecifics()
    {
        using var db = TestDbContextFactory.Create();
        var controller = NewRoleGroupsController(db);

        var req = new RoleGroupsController.SaveRoleGroupRequest
        {
            NameEn      = "ModuleWildcard",
            // Process.* should absorb Process.View / Process.Edit.
            // Risk.View has no Risk.* wildcard, so it survives.
            Permissions = "Process.*,Process.View,Process.Edit,Risk.View",
        };
        var result = await controller.Save(req);
        Assert.True((bool)ToDict(((JsonResult)result).Value)["success"]!);

        var saved = await db.RoleGroups.AsNoTracking()
            .FirstAsync(g => g.NameEn == "ModuleWildcard");
        Assert.Equal("Process.*,Risk.View", saved.Permissions);
    }

    /// <summary>
    /// Walks an anonymous-object Save result into a dictionary. Each Save
    /// returns either { success, id } or { success, error } — both have
    /// public properties we can grab via reflection.
    /// </summary>
    private static IDictionary<string, object?> ToDict(object? src)
    {
        if (src == null) return new Dictionary<string, object?>();
        var dict = new Dictionary<string, object?>();
        foreach (var p in src.GetType().GetProperties())
            dict[p.Name] = p.GetValue(src);
        return dict;
    }
}
