using System.Security.Claims;
using ESEMS.Web;
using ESEMS.Web.Controllers;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.ServiceManagement;
using ESEMS.Web.Models.Services;
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
/// Regression tests for the bigTest data-scoping LIST/aggregate leaks (the
/// sibling of <see cref="IdorScopeGuardTests"/>, which covers single-record
/// IDOR). These prove that dashboard KPI counts and recent-item lists respect
/// the caller's org-unit scope — a scoped user must not see org-wide totals or
/// recent rows from units outside their tree. Invoked controller-direct with an
/// in-memory DbContext, the real <see cref="ScopingService"/>, and a forged
/// scoped principal.
/// </summary>
public class ScopeLeakTests
{
    private const int UnitA = 9001; // in scope
    private const int UnitB = 9002; // out of scope

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

    [Fact]
    public async Task ProcessesDashboard_KpiCounts_AreScopeFiltered()
    {
        using var db = TestDbContextFactory.Create();
        db.Processes.Add(new Process { Id = "p-a", Code = "P-A", NameEn = "a", NameAr = "أ", OwningUnitId = UnitA });
        db.Processes.Add(new Process { Id = "p-b", Code = "P-B", NameEn = "b", NameAr = "ب", OwningUnitId = UnitB });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new ProcessesController(
            db, NullLogger<ProcessesController>.Instance, new FakeLocalizer<SharedResource>(),
            new ScopingService(db, cache), codeSvc: null!);
        Attach(controller, Principal("OwningUnit", UnitA));

        Assert.IsType<ViewResult>(await controller.Dashboard());
        Assert.Equal(1, (int)controller.ViewBag.TotalProcesses); // was org-wide 2
    }

    [Fact]
    public async Task ServicesDashboard_KpiCounts_AreScopeFiltered()
    {
        using var db = TestDbContextFactory.Create();
        db.Services.Add(new Service { Id = "s-a", Code = "S-A", NameEn = "a", NameAr = "أ", OwningUnitId = UnitA });
        db.Services.Add(new Service { Id = "s-b", Code = "S-B", NameEn = "b", NameAr = "ب", OwningUnitId = UnitB });
        db.Incidents.Add(new Incident { Id = "i-a", IncidentNumber = "INC-A", NameEn = "a", NameAr = "أ", AssignedToUnitId = UnitA, Status = IncidentStatus.New });
        db.Incidents.Add(new Incident { Id = "i-b", IncidentNumber = "INC-B", NameEn = "b", NameAr = "ب", AssignedToUnitId = UnitB, Status = IncidentStatus.New });
        db.Problems.Add(new Problem { Id = "pr-a", ProblemNumber = "PRB-A", NameEn = "a", NameAr = "أ", AssignedToUnitId = UnitA });
        db.Problems.Add(new Problem { Id = "pr-b", ProblemNumber = "PRB-B", NameEn = "b", NameAr = "ب", AssignedToUnitId = UnitB });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new ServicesController(
            db, NullLogger<ServicesController>.Instance, new FakeLocalizer<SharedResource>(),
            new ScopingService(db, cache), codeSvc: null!);
        Attach(controller, Principal("OwningUnit", UnitA));

        Assert.IsType<ViewResult>(await controller.Dashboard());
        Assert.Equal(1, (int)controller.ViewBag.TotalServices);  // was 2
        Assert.Equal(1, (int)controller.ViewBag.TotalIncidents); // was 2
        Assert.Equal(1, (int)controller.ViewBag.TotalProblems);  // was 2
    }
}
