using System.Linq;
using System.Security.Claims;
using ESEMS.Web;
using ESEMS.Web.Controllers;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.RiskManagement;
using ESEMS.Web.Models.ServiceManagement;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.WorkloadAnalysis;
using ESEMS.Web.Services.Common;
using ESEMS.Tests.TestFixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ESEMS.Tests.Security;

/// <summary>
/// Controller-level regression tests for the bigTest per-record IDOR guards
/// (commit f70f069). These invoke the controller actions directly with an
/// in-memory DbContext, the real <see cref="ScopingService"/>, and a forged
/// scoped principal — exercising exactly the guard logic
/// (load → GetScopeAsync → CanAccess → NotFound) without the HTTP pipeline.
///
///   F-001  Improvements/Transition refuses to set Approved/Rejected (it's
///          gated on Improvement.Edit, which Editors hold without Approve).
///   F-003  WorkloadAnalysis/Details returns NotFound for a scenario whose
///          owning unit is outside the caller's scope; renders for in-scope.
///   F-020  Tasks/Details returns NotFound for a task whose parent process is
///          owned by another unit (CanAccessTask scopes via the parent).
///
/// F-006/F-007/F-008/F-019 share the identical idiom and the same
/// ScopeContextExtensions.CanAccess primitive proven here.
///
/// Note on seeding: these Details actions Include *required* reference
/// navigations (WorkloadScenario→Config, ProcessTask→Activity→Process→Group).
/// EF's in-memory provider filters the root row out of an Include when a
/// required principal is missing (relational SQL Server does a LEFT JOIN and
/// returns it), so the required graph is seeded for the row to be found at all.
/// </summary>
public class IdorScopeGuardTests
{
    private const int UnitA = 9001;
    private const int UnitB = 9002;

    // ── Test doubles / helpers ───────────────────────────────────────────

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

    /// <summary>Forge a principal. scopeLevel=null ⇒ unscoped (admin) like "All".</summary>
    private static ClaimsPrincipal Principal(string? scopeLevel, int? orgUnitId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Name, "tester"),
            new(ClaimTypes.Role, "VIEWER"),
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

    // ── F-003: WorkloadAnalysis/Details ───────────────────────────────────

    [Fact]
    public async Task F003_WorkloadDetails_BlocksOutOfScope_AllowsInScope()
    {
        using var db = TestDbContextFactory.Create();
        db.WorkloadConfigs.Add(new WorkloadConfig { Id = "cfg-1", NameEn = "cfg", NameAr = "إعداد" });
        db.WorkloadScenarios.Add(new WorkloadScenario { Id = "ws-a", Code = "WS-A", NameEn = "A", NameAr = "أ", OwningUnitId = UnitA, WorkloadConfigId = "cfg-1", IsDeleted = false });
        db.WorkloadScenarios.Add(new WorkloadScenario { Id = "ws-b", Code = "WS-B", NameEn = "B", NameAr = "ب", OwningUnitId = UnitB, WorkloadConfigId = "cfg-1", IsDeleted = false });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new WorkloadAnalysisController(
            db, NullLogger<WorkloadAnalysisController>.Instance, new FakeLocalizer<SharedResource>(), new ScopingService(db, cache));
        Attach(controller, Principal("OwningUnit", UnitA));

        // Out of scope (owned by B) → 404, no existence leak.
        Assert.IsType<NotFoundResult>(await controller.Details("ws-b"));

        // In scope (owned by A) → not blocked (renders a ViewResult).
        Assert.IsNotType<NotFoundResult>(await controller.Details("ws-a"));
    }

    // ── F-020: Tasks/Details (scoped via parent process) ──────────────────

    [Fact]
    public async Task F020_TaskDetails_BlocksOutOfScope_AllowsInScope()
    {
        using var db = TestDbContextFactory.Create();
        db.ProcessGroups.Add(new ProcessGroup { Id = "pg-1", Code = "PG-1", NameEn = "g", NameAr = "ج", CategoryId = "cat-1" });
        db.Processes.Add(new Process { Id = "p-a", Code = "P-A", NameEn = "pa", NameAr = "با", ProcessGroupId = "pg-1", OwningUnitId = UnitA });
        db.Processes.Add(new Process { Id = "p-b", Code = "P-B", NameEn = "pb", NameAr = "بب", ProcessGroupId = "pg-1", OwningUnitId = UnitB });
        db.Activities.Add(new Activity { Id = "act-a", Code = "AC-A", NameEn = "aa", NameAr = "آ", ProcessId = "p-a" });
        db.Activities.Add(new Activity { Id = "act-b", Code = "AC-B", NameEn = "ab", NameAr = "إ", ProcessId = "p-b" });
        db.ProcessTasks.Add(new ProcessTask { Id = "task-a", Code = "T-A", NameEn = "A", NameAr = "أ", ActivityId = "act-a", OwningUnitId = UnitA, IsDeleted = false });
        db.ProcessTasks.Add(new ProcessTask { Id = "task-b", Code = "T-B", NameEn = "B", NameAr = "ب", ActivityId = "act-b", OwningUnitId = UnitB, IsDeleted = false });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new TasksController(
            db, NullLogger<TasksController>.Instance, new FakeLocalizer<SharedResource>(), new ScopingService(db, cache));
        Attach(controller, Principal("OwningUnit", UnitA));

        // task-b's parent process (p-b) is owned by B → out of scope → 404.
        Assert.IsType<NotFoundResult>(await controller.Details("task-b"));

        // task-a's parent process (p-a) is owned by A → in scope.
        Assert.IsNotType<NotFoundResult>(await controller.Details("task-a"));
    }

    // ── F-001: Improvements/Transition is not an approval back-door ────────

    [Fact]
    public async Task F001_Transition_CannotSetApproved()
    {
        using var db = TestDbContextFactory.Create();
        db.ImprovementInitiatives.Add(new ImprovementInitiative
        {
            Id = "imp-1",
            Code = "INI-001",
            TitleEn = "t", TitleAr = "ت",
            NameEn = "t", NameAr = "ت",
            Status = ImprovementStatus.UnderReview,
            OwningUnitId = null,  // orphan ⇒ scope check passes; isolates the RBAC concern
            OwnerId = null        // ⇒ no notification path
        });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        // Transition only touches _context + _scopingService (the F-001 block
        // returns before any workflow/notification/code/export service is used),
        // so the unused dependencies can be null here.
        var controller = new ImprovementsController(
            db,
            NullLogger<ImprovementsController>.Instance,
            new FakeLocalizer<SharedResource>(),
            improvementService: null!,
            workflowService: null!,
            notificationService: null!,
            measurementCollection: null!,
            scopingService: new ScopingService(db, cache),
            codeSvc: null!,
            exportSvc: null!);
        Attach(controller, Principal(scopeLevel: null, orgUnitId: null)); // unscoped editor

        var result = await controller.Transition("imp-1", "Approved", null);

        // Blocked → redirect back to Details, not an FSM success.
        Assert.IsType<RedirectToActionResult>(result);

        // The decisive check: the initiative was NOT approved.
        var after = await db.ImprovementInitiatives.FindAsync("imp-1");
        Assert.NotNull(after);
        Assert.Equal(ImprovementStatus.UnderReview, after!.Status);
    }

    // ── F-019: AIController/AnalyzeProcess record scope ────────────────────

    [Fact]
    public async Task F019_AnalyzeProcess_OutOfScope_Returns404()
    {
        using var db = TestDbContextFactory.Create();
        // AnalyzeProcess loads the process WITHOUT an Include, so no chain needed.
        db.Processes.Add(new Process { Id = "p-ai-b", Code = "P-AIB", NameEn = "pb", NameAr = "بب", OwningUnitId = UnitB });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        // Only _context + _scopingService are reached before the scope guard
        // returns; the AI/BPMN/analysis services are never touched, so null.
        var controller = new AIController(
            aiService: null!, db, NullLogger<AIController>.Instance,
            bpmnService: null!, analysisService: null!, cache, new ScopingService(db, cache));
        Attach(controller, Principal("OwningUnit", UnitA));

        var result = await controller.AnalyzeProcess(new ProcessAnalysisRequest { ProcessId = "p-ai-b" });
        Assert.IsType<NotFoundResult>(result);
    }

    // ── AI IDOR guards added by the scoping audit ─────────────────────────
    // Each loads a scoped entity by id then analyzes it; an out-of-scope id
    // must 404 BEFORE the (null) AI service is touched.

    private static AIController NewAi(ApplicationDbContext db, IMemoryCache cache) =>
        new(aiService: null!, db, NullLogger<AIController>.Instance,
            bpmnService: null!, analysisService: null!, cache, new ScopingService(db, cache));

    [Fact]
    public async Task GenerateProcessImprovements_OutOfScope_Returns404()
    {
        using var db = TestDbContextFactory.Create();
        db.Processes.Add(new Process { Id = "gpi-b", Code = "GPI-B", NameEn = "p", NameAr = "ب", OwningUnitId = UnitB });
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var c = NewAi(db, cache);
        Attach(c, Principal("OwningUnit", UnitA));
        Assert.IsType<NotFoundResult>(await c.GenerateProcessImprovements("gpi-b"));
    }

    [Fact]
    public async Task AnalyzeProblem_OutOfScope_Returns404()
    {
        using var db = TestDbContextFactory.Create();
        db.Problems.Add(new Problem { Id = "prob-b", ProblemNumber = "PRB-B", NameEn = "p", NameAr = "ب", AssignedToUnitId = UnitB, IsDeleted = false });
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var c = NewAi(db, cache);
        Attach(c, Principal("OwningUnit", UnitA));
        Assert.IsType<NotFoundResult>(await c.AnalyzeProblem("prob-b"));
    }

    [Fact]
    public async Task AnalyzeChangeRequest_OutOfScope_Returns404()
    {
        using var db = TestDbContextFactory.Create();
        db.ChangeRequests.Add(new ChangeRequest { Id = "cr-b", Code = "CR-B", NameEn = "c", NameAr = "ب", OwningUnitId = UnitB, IsDeleted = false });
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var c = NewAi(db, cache);
        Attach(c, Principal("OwningUnit", UnitA));
        Assert.IsType<NotFoundResult>(await c.AnalyzeChangeRequest("cr-b"));
    }

    [Fact]
    public async Task AnalyzeRisk_OnOutOfScopeProcess_Returns404()
    {
        using var db = TestDbContextFactory.Create();
        // ProcessRisk scopes via its parent process (owned by B → out of scope).
        db.Processes.Add(new Process { Id = "prp-b", Code = "PRP-B", NameEn = "p", NameAr = "ب", OwningUnitId = UnitB });
        db.ProcessRisks.Add(new ProcessRisk { Id = "prisk-b", ProcessId = "prp-b", NameEn = "r", NameAr = "ر" });
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var c = NewAi(db, cache);
        Attach(c, Principal("OwningUnit", UnitA));
        Assert.IsType<NotFoundResult>(await c.AnalyzeRisk("prisk-b"));
    }

    // ── F-008: AssetsController/GetAvailableRisks record scope ─────────────

    [Fact]
    public async Task F008_GetAvailableRisks_OutOfScope_IsEmpty_InScope_Lists()
    {
        using var db = TestDbContextFactory.Create();
        // RiskCategory seeded so GetAvailableRisks' .Include(r => r.Category)
        // (a required reference) doesn't filter the risk under EF in-memory.
        db.RiskCategories.Add(new RiskCategory { Id = "rc", Code = "RC", NameEn = "c", NameAr = "ف" });
        db.Assets.Add(new Asset { Id = "asset-b", AssetTag = "AST-B", NameEn = "a", NameAr = "أ", CategoryId = "cat", AssignedToUnitId = UnitB, IsDeleted = false });
        db.EnterpriseRisks.Add(new EnterpriseRisk { Id = "risk-1", RiskNumber = "R-1", NameEn = "r", NameAr = "ر", CategoryId = "rc", IsActive = true, IsDeleted = false });
        await db.SaveChangesAsync();

        // Scoped to A → asset owned by B is out of scope → empty list (no leak).
        using var c1 = new MemoryCache(new MemoryCacheOptions());
        var scoped = new AssetsController(db, NullLogger<AssetsController>.Instance, new FakeLocalizer<SharedResource>(), new ScopingService(db, c1));
        Attach(scoped, Principal("OwningUnit", UnitA));
        var scopedJson = Assert.IsType<JsonResult>(await scoped.GetAvailableRisks("asset-b"));
        Assert.Empty(((System.Collections.IEnumerable)scopedJson.Value!).Cast<object>());

        // Unscoped admin → guard passes → the available risk is listed.
        using var c2 = new MemoryCache(new MemoryCacheOptions());
        var admin = new AssetsController(db, NullLogger<AssetsController>.Instance, new FakeLocalizer<SharedResource>(), new ScopingService(db, c2));
        Attach(admin, Principal(scopeLevel: null, orgUnitId: null));
        var adminJson = Assert.IsType<JsonResult>(await admin.GetAvailableRisks("asset-b"));
        Assert.NotEmpty(((System.Collections.IEnumerable)adminJson.Value!).Cast<object>());
    }

    // ── FU-001: WorkloadAnalysis GetProcessData/GetServiceData record scope ─

    [Fact]
    public async Task FU001_WorkloadGetData_OutOfScope_Returns404()
    {
        using var db = TestDbContextFactory.Create();
        db.Processes.Add(new Process { Id = "wproc-b", Code = "WP-B", NameEn = "p", NameAr = "ب", OwningUnitId = UnitB });
        db.Services.Add(new Service { Id = "wsvc-b", Code = "WS-B", NameEn = "s", NameAr = "خ", OwningUnitId = UnitB });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new WorkloadAnalysisController(
            db, NullLogger<WorkloadAnalysisController>.Instance, new FakeLocalizer<SharedResource>(), new ScopingService(db, cache));
        Attach(controller, Principal("OwningUnit", UnitA));

        Assert.IsType<NotFoundResult>(await controller.GetProcessData("wproc-b"));
        Assert.IsType<NotFoundResult>(await controller.GetServiceData("wsvc-b"));
    }

    // ── Perf/scope: Improvements Index quadrant counts are scope-filtered ──

    [Fact]
    public async Task ImprovementsIndex_QuadrantCounts_AreScopeFiltered()
    {
        using var db = TestDbContextFactory.Create();
        ImprovementInitiative Imp(string id, int unit) => new()
        {
            Id = id, Code = "INI-" + id, TitleEn = "t", TitleAr = "ت", NameEn = "t", NameAr = "ت",
            Quadrant = ImprovementQuadrant.QuickWins, OwningUnitId = unit
        };
        db.ImprovementInitiatives.Add(Imp("a1", UnitA));
        db.ImprovementInitiatives.Add(Imp("a2", UnitA));
        db.ImprovementInitiatives.Add(Imp("b1", UnitB)); // out of scope
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new ImprovementsController(
            db, NullLogger<ImprovementsController>.Instance, new FakeLocalizer<SharedResource>(),
            null!, null!, null!, null!, new ScopingService(db, cache), null!, null!);
        Attach(controller, Principal("OwningUnit", UnitA));

        var result = Assert.IsType<ViewResult>(await controller.Index(null, null, null));

        // Was org-wide 3 (full-table in-memory count); now the scoped 2.
        Assert.Equal(2, (int)controller.ViewBag.QuickWinsCount);
        var model = Assert.IsAssignableFrom<IEnumerable<ImprovementInitiative>>(result.Model);
        Assert.Equal(2, model.Count());
    }
}
