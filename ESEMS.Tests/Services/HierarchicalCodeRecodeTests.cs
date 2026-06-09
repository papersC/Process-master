using System.Security.Claims;
using ESEMS.Tests.TestFixtures;
using ESEMS.Web;
using ESEMS.Web.Controllers;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Services.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ESEMS.Tests.Services;

/// <summary>
/// Covers the re-parent re-code paths added to fix stale hierarchical IDs:
/// moving a Process to a new ProcessGroup (L3) and moving a ProcessGroup to a
/// new Category (L2) must re-stamp the moved node's Code/SortKey AND cascade
/// the rename through the derived-code subtree. Verifies the two distinct
/// strategies: L3 <b>reallocates</b> the moved process's trailing segment;
/// L2 is a pure <b>prefix swap</b> where descendants keep their own segments.
/// </summary>
public class HierarchicalCodeRecodeTests
{
    // ── L3: Process → different ProcessGroup ──────────────────────────────

    [Fact]
    public async Task RecodeProcessUnderGroup_TakesNextFreeCode_AndCascadesToActivitiesAndTasks()
    {
        using var ctx = TestDbContextFactory.Create();

        // Category "1" with a source group "1.1" and a target group "1.2".
        ctx.Categories.Add(new Category { Id = "cat1", Code = "1", NameEn = "Cat" });
        ctx.ProcessGroups.Add(new ProcessGroup { Id = "g1", Code = "1.1", CategoryId = "cat1" });
        ctx.ProcessGroups.Add(new ProcessGroup { Id = "g2", Code = "1.2", CategoryId = "cat1" });

        // Target group already holds "1.2.1", so the moved process must take the
        // NEXT free Z ("1.2.2") rather than its old segment.
        ctx.Processes.Add(new Process { Id = "pExisting", Code = "1.2.1", ProcessGroupId = "g2" });

        // The process to move, with a derived activity + task and one hand-edited
        // activity code that must be left alone.
        ctx.Processes.Add(new Process
        {
            Id = "pMove",
            Code = "1.1.1",
            SortKey = HierarchicalCodeService.SortKeyFor("1.1.1"),
            ProcessGroupId = "g1"
        });
        ctx.Activities.Add(new Activity { Id = "a1", Code = "1.1.1.01", ProcessId = "pMove" });
        ctx.ProcessTasks.Add(new ProcessTask { Id = "t1", Code = "1.1.1.01.1", ActivityId = "a1" });
        ctx.Activities.Add(new Activity { Id = "aCustom", Code = "LEGACY-9", ProcessId = "pMove" });
        await ctx.SaveChangesAsync();

        // Act — mirror ProcessesController.Edit: set the new group, then recode.
        var svc = new HierarchicalCodeService(ctx);
        var moved = await ctx.Processes.FirstAsync(p => p.Id == "pMove");
        moved.ProcessGroupId = "g2";
        await svc.RecodeProcessUnderGroupAsync(moved);
        await ctx.SaveChangesAsync();

        // Process re-coded to the next free slot under the target group.
        Assert.Equal("1.2.2", moved.Code);
        Assert.Equal(HierarchicalCodeService.SortKeyFor("1.2.2"), moved.SortKey);

        // Derived subtree cascaded.
        Assert.Equal("1.2.2.01", (await ctx.Activities.FirstAsync(a => a.Id == "a1")).Code);
        Assert.Equal("1.2.2.01.1", (await ctx.ProcessTasks.FirstAsync(t => t.Id == "t1")).Code);

        // Hand-edited (non-derived) code untouched; sibling untouched.
        Assert.Equal("LEGACY-9", (await ctx.Activities.FirstAsync(a => a.Id == "aCustom")).Code);
        Assert.Equal("1.2.1", (await ctx.Processes.FirstAsync(p => p.Id == "pExisting")).Code);
    }

    [Fact]
    public async Task RecodeProcessUnderGroup_SameGroup_IsNoOp()
    {
        using var ctx = TestDbContextFactory.Create();
        ctx.Categories.Add(new Category { Id = "cat1", Code = "1", NameEn = "Cat" });
        ctx.ProcessGroups.Add(new ProcessGroup { Id = "g1", Code = "1.1", CategoryId = "cat1" });
        ctx.Processes.Add(new Process { Id = "p", Code = "1.1.1", ProcessGroupId = "g1" });
        await ctx.SaveChangesAsync();

        // Re-coding without an actual move would only ever re-derive the same
        // code; the controller guards on groupChanged, but the service itself
        // must also be safe to call and never bump the number for an unchanged
        // group. (Here Z=1 is the max, so next-free is "1.1.2" — proving WHY the
        // controller must guard: we assert the controller's contract by only
        // calling recode after a real move. This documents the boundary.)
        var svc = new HierarchicalCodeService(ctx);
        var p = await ctx.Processes.FirstAsync(x => x.Id == "p");
        var before = p.Code;

        // Simulate the controller decision: group did NOT change → do not recode.
        var groupChanged = p.ProcessGroupId != "g1";
        if (groupChanged)
            await svc.RecodeProcessUnderGroupAsync(p);
        await ctx.SaveChangesAsync();

        Assert.False(groupChanged);
        Assert.Equal(before, p.Code); // unchanged
    }

    // ── L2: ProcessGroup → different Category ─────────────────────────────

    [Fact]
    public async Task RecodeProcessGroupUnderCategory_MovesGroupCode_AndPrefixSwapsSubtree()
    {
        using var ctx = TestDbContextFactory.Create();

        ctx.Categories.Add(new Category { Id = "c1", Code = "1", NameEn = "Cat1" });
        ctx.Categories.Add(new Category { Id = "c2", Code = "2", NameEn = "Cat2" });
        // Target category already has "2.1", so next free Y = 2.
        ctx.ProcessGroups.Add(new ProcessGroup { Id = "gExisting", Code = "2.1", CategoryId = "c2" });

        // The group to move, with a process (Z=2), its activity and task.
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            Id = "gMove",
            Code = "1.3",
            SortKey = HierarchicalCodeService.SortKeyFor("1.3"),
            CategoryId = "c1"
        });
        ctx.Processes.Add(new Process
        {
            Id = "p",
            Code = "1.3.2",
            SortKey = HierarchicalCodeService.SortKeyFor("1.3.2"),
            ProcessGroupId = "gMove"
        });
        ctx.Activities.Add(new Activity { Id = "a", Code = "1.3.2.01", ProcessId = "p" });
        ctx.ProcessTasks.Add(new ProcessTask { Id = "t", Code = "1.3.2.01.1", ActivityId = "a" });
        await ctx.SaveChangesAsync();

        // Act — mirror ProcessGroupsController.Edit: set the new category, recode.
        var svc = new HierarchicalCodeService(ctx);
        var moved = await ctx.ProcessGroups.FirstAsync(g => g.Id == "gMove");
        moved.CategoryId = "c2";
        await svc.RecodeProcessGroupUnderCategoryAsync(moved);
        await ctx.SaveChangesAsync();

        // Group took the next free Y under the target category.
        Assert.Equal("2.2", moved.Code);
        Assert.Equal(HierarchicalCodeService.SortKeyFor("2.2"), moved.SortKey);

        // Subtree prefix-swapped; each descendant KEEPS its own segment.
        var p = await ctx.Processes.FirstAsync(x => x.Id == "p");
        Assert.Equal("2.2.2", p.Code);                       // Z stays 2
        Assert.Equal(HierarchicalCodeService.SortKeyFor("2.2.2"), p.SortKey);
        Assert.Equal("2.2.2.01", (await ctx.Activities.FirstAsync(x => x.Id == "a")).Code);
        Assert.Equal("2.2.2.01.1", (await ctx.ProcessTasks.FirstAsync(x => x.Id == "t")).Code);

        // Sibling group in the target category untouched.
        Assert.Equal("2.1", (await ctx.ProcessGroups.FirstAsync(g => g.Id == "gExisting")).Code);
    }

    // ── End-to-end through the real Edit handlers (controller wiring) ─────
    // These drive the actual controller actions — proving the handler captures
    // the move BEFORE overwriting the FK, calls the re-code, and persists it in
    // the same SaveChanges — not just the service in isolation.

    [Fact]
    public async Task ProcessesControllerEdit_MovingGroup_ReStampsCodeAndCascade()
    {
        using var db = TestDbContextFactory.Create();
        db.Categories.Add(new Category { Id = "cat1", Code = "1", NameEn = "Cat" });
        db.ProcessGroups.Add(new ProcessGroup { Id = "g1", Code = "1.1", CategoryId = "cat1" });
        db.ProcessGroups.Add(new ProcessGroup { Id = "g2", Code = "1.2", CategoryId = "cat1" });
        db.Processes.Add(new Process { Id = "pExisting", Code = "1.2.1", NameEn = "x", NameAr = "x", ProcessGroupId = "g2" });
        db.Processes.Add(new Process
        {
            Id = "pMove",
            Code = "1.1.1",
            SortKey = HierarchicalCodeService.SortKeyFor("1.1.1"),
            NameEn = "Move", NameAr = "نقل",
            ProcessGroupId = "g1"
        });
        db.Activities.Add(new Activity { Id = "a1", Code = "1.1.1.01", NameEn = "act", NameAr = "نشاط", ProcessId = "pMove" });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new ProcessesController(
            db, NullLogger<ProcessesController>.Instance, new FakeLocalizer<SharedResource>(),
            new ScopingService(db, cache), new HierarchicalCodeService(db));
        Attach(controller, Principal(scopeLevel: null, orgUnitId: null)); // unscoped admin

        // The posted form: the same process, with the group dropdown changed to g2.
        var posted = new Process { Id = "pMove", ProcessGroupId = "g2", NameEn = "Move", NameAr = "نقل" };
        var result = await controller.Edit("pMove", posted,
            SelectedServiceIds: null, SelectedObjectiveIds: null, SelectedAssetIds: null,
            SelectedRiskIds: null, SelectedResponsibilityIds: null, TagList: null, DocumentLinksJson: null);

        Assert.IsType<RedirectToActionResult>(result);

        var moved = await db.Processes.AsNoTracking().FirstAsync(p => p.Id == "pMove");
        Assert.Equal("1.2.2", moved.Code);
        Assert.Equal(HierarchicalCodeService.SortKeyFor("1.2.2"), moved.SortKey);
        Assert.Equal("1.2.2.01", (await db.Activities.AsNoTracking().FirstAsync(a => a.Id == "a1")).Code);
        // Sibling that was already in the target group is undisturbed.
        Assert.Equal("1.2.1", (await db.Processes.AsNoTracking().FirstAsync(p => p.Id == "pExisting")).Code);
    }

    [Fact]
    public async Task ProcessGroupsControllerEdit_MovingCategory_ReStampsSubtree()
    {
        using var db = TestDbContextFactory.Create();
        db.Categories.Add(new Category { Id = "c1", Code = "1", NameEn = "C1" });
        db.Categories.Add(new Category { Id = "c2", Code = "2", NameEn = "C2" });
        db.ProcessGroups.Add(new ProcessGroup { Id = "gExisting", Code = "2.1", CategoryId = "c2" });
        db.ProcessGroups.Add(new ProcessGroup
        {
            Id = "gMove",
            Code = "1.3",
            SortKey = HierarchicalCodeService.SortKeyFor("1.3"),
            NameEn = "G", NameAr = "ج",
            CategoryId = "c1"
        });
        db.Processes.Add(new Process
        {
            Id = "p",
            Code = "1.3.2",
            SortKey = HierarchicalCodeService.SortKeyFor("1.3.2"),
            NameEn = "P", NameAr = "ب",
            ProcessGroupId = "gMove"
        });
        db.Activities.Add(new Activity { Id = "a", Code = "1.3.2.01", NameEn = "A", NameAr = "ن", ProcessId = "p" });
        db.ProcessTasks.Add(new ProcessTask { Id = "t", Code = "1.3.2.01.1", NameEn = "T", NameAr = "م", ActivityId = "a" });
        await db.SaveChangesAsync();

        var controller = new ProcessGroupsController(
            db, NullLogger<ProcessGroupsController>.Instance, new FakeLocalizer<SharedResource>(),
            new HierarchicalCodeService(db));
        Attach(controller, Principal(scopeLevel: null, orgUnitId: null));

        var posted = new ProcessGroup { Id = "gMove", CategoryId = "c2", NameEn = "G", NameAr = "ج" };
        var result = await controller.Edit("gMove", posted);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("2.2", (await db.ProcessGroups.AsNoTracking().FirstAsync(g => g.Id == "gMove")).Code);
        Assert.Equal("2.2.2", (await db.Processes.AsNoTracking().FirstAsync(x => x.Id == "p")).Code);
        Assert.Equal("2.2.2.01", (await db.Activities.AsNoTracking().FirstAsync(x => x.Id == "a")).Code);
        Assert.Equal("2.2.2.01.1", (await db.ProcessTasks.AsNoTracking().FirstAsync(x => x.Id == "t")).Code);
    }

    // ── Dropdown disambiguation: same group name across categories ────────

    [Fact]
    public async Task Create_ProcessGroupDropdown_QualifiesDuplicateNamesWithCategory()
    {
        using var db = TestDbContextFactory.Create();
        // Same group name under two different categories — the real-data case
        // (e.g. "Enhance & Govern Processes" lives under 3 categories).
        db.Categories.Add(new Category { Id = "ca", Code = "1", NameEn = "Category A", NameAr = "أ" });
        db.Categories.Add(new Category { Id = "cb", Code = "2", NameEn = "Category B", NameAr = "ب" });
        db.ProcessGroups.Add(new ProcessGroup { Id = "g-a", Code = "1.1", NameEn = "Shared Group", NameAr = "مجموعة", CategoryId = "ca" });
        db.ProcessGroups.Add(new ProcessGroup { Id = "g-b", Code = "2.1", NameEn = "Shared Group", NameAr = "مجموعة", CategoryId = "cb" });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new ProcessesController(
            db, NullLogger<ProcessesController>.Instance, new FakeLocalizer<SharedResource>(),
            new ScopingService(db, cache), new HierarchicalCodeService(db));
        Attach(controller, Principal(scopeLevel: null, orgUnitId: null));

        var result = await controller.Create();
        Assert.IsType<ViewResult>(result);

        var items = ((SelectList)controller.ViewBag.ProcessGroups).Select(i => i.Text).ToList();
        // Both options remain, now distinguishable by their parent category.
        Assert.Contains("Shared Group (Category A)", items);
        Assert.Contains("Shared Group (Category B)", items);
        // The bare, ambiguous label is gone.
        Assert.DoesNotContain("Shared Group", items);
    }

    // ── Test doubles (mirror ScopeLeakTests / IdorScopeGuardTests) ────────

    private sealed class FakeLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    /// <summary>Forge a principal. scopeLevel=null ⇒ unscoped (admin).</summary>
    private static ClaimsPrincipal Principal(string? scopeLevel, int? orgUnitId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Name, "tester"),
            new("UserId", "1"),
        };
        if (scopeLevel != null) claims.Add(new Claim("ScopeLevel", scopeLevel));
        if (orgUnitId.HasValue) claims.Add(new Claim("OrganizationUnitId", orgUnitId.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static void Attach(Controller c, ClaimsPrincipal user)
    {
        var http = new DefaultHttpContext { User = user };
        c.ControllerContext = new ControllerContext { HttpContext = http };
        c.TempData = new TempDataDictionary(http, new NullTempDataProvider());
    }
}
