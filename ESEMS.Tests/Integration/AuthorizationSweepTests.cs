using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Adversarial authorization sweep — top risk #1.
///
/// What this proves:
///   * Every controller's primary GET requires authentication (no missing
///     [Authorize], no class-level [AllowAnonymous] override regression —
///     specifically locks the AccountController.Profile fix).
///   * The login surface stays anonymous-reachable (don't break login).
///   * Cross-module permission isolation (Asset.View claim cannot reach
///     Improvement.* routes).
///   * Action-level isolation within a module (X.View cannot Create/Edit/
///     Delete — verb scoping holds).
///
/// What this does NOT prove (intentional gaps):
///   * Resource-level IDOR (no ownership claim infra exists today).
///   * RACI/owner-bound checks.
/// </summary>
public class AuthorizationSweepTests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public AuthorizationSweepTests(EsemsTestFactory factory) { _factory = factory; }

    private HttpClient Anon() => CreateClient(role: "ANON");
    private HttpClient WithPerms(params string[] perms) => CreateClient(role: "VIEWER", perms: string.Join(",", perms));

    private HttpClient CreateClient(string role, string? perms = null)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Role", role);
        if (perms != null) c.DefaultRequestHeaders.Add("X-Test-Perms", perms);
        return c;
    }

    // ---------------------------------------------------------------------
    // 1) Anonymous sweep — every protected GET must reject anonymous traffic.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("/Account/Profile")]            // regression for ASP0026 fix
    [InlineData("/Categories")]
    [InlineData("/Services")]
    [InlineData("/Processes")]
    [InlineData("/Incidents")]
    [InlineData("/EnterpriseRisks")]
    [InlineData("/Assets")]
    [InlineData("/AssetCategories")]
    [InlineData("/Users")]
    [InlineData("/RoleGroups")]
    [InlineData("/SLA")]
    [InlineData("/WorkloadAnalysis")]
    [InlineData("/Workflow/PendingApprovals")]
    [InlineData("/Improvements")]
    [InlineData("/ChangeRequests")]
    [InlineData("/Problems")]
    [InlineData("/AuditLogs")]
    [InlineData("/SettingsHub")]
    [InlineData("/SystemInfo")]
    [InlineData("/Help")]
    [InlineData("/AI/Diagrams")]
    [InlineData("/MySpace")]
    [InlineData("/Activities")]
    [InlineData("/api/notifications")]
    [InlineData("/api/users/active")]
    public async Task Anonymous_Cannot_Access_Protected_Routes(string path)
    {
        var r = await Anon().GetAsync(path);
        Assert.True(
            r.StatusCode is HttpStatusCode.Unauthorized
                        or HttpStatusCode.Forbidden
                        or HttpStatusCode.Redirect
                        or HttpStatusCode.Found,
            $"GET {path} should reject anonymous (401/403/302). Got {(int)r.StatusCode}.");
    }

    // ---------------------------------------------------------------------
    // 2) Login surface MUST stay anonymous-accessible.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/AccessDenied")]
    [InlineData("/health")]
    public async Task Anonymous_Can_Reach_Login_Surface(string path)
    {
        var r = await Anon().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // ---------------------------------------------------------------------
    // 3) Cross-module isolation — Asset.View must not unlock Improvement /
    //    EnterpriseRisk / etc. Plan-X policies are per-module, not additive.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("/Improvements")]
    [InlineData("/EnterpriseRisks")]
    [InlineData("/Incidents")]
    [InlineData("/Problems")]
    [InlineData("/ChangeRequests")]
    [InlineData("/Users")]
    public async Task AssetViewer_Cannot_Reach_OtherModule_Routes(string foreignPath)
    {
        var c = WithPerms("Asset.View");
        var r = await c.GetAsync(foreignPath);
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    // ---------------------------------------------------------------------
    // 4) Verb-scoped isolation — within a module, View must not unlock
    //    Create/Edit/Delete. Catches a regression where "Asset.*" gets
    //    coerced into all verbs.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("/Assets/Create", "Asset")]
    [InlineData("/AssetCategories/Create", "Asset")]
    [InlineData("/Improvements/Wizard", "Improvement")]
    [InlineData("/EnterpriseRisks/Create", "Risk")]
    [InlineData("/Incidents/Create", "Incident")]
    public async Task ViewOnly_Cannot_Reach_Create_Endpoints(string createPath, string permModule)
    {
        var c = WithPerms($"{permModule}.View");
        var r = await c.GetAsync(createPath);
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    // ---------------------------------------------------------------------
    // 5) CSRF: state-changing POSTs must reject without an antiforgery token.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("/Improvements/CreateFromWizard")]
    [InlineData("/Incidents/Create")]
    [InlineData("/EnterpriseRisks/Create")]
    public async Task Authenticated_Post_Without_Antiforgery_IsRejected(string postPath)
    {
        var c = WithPerms("*.*");
        c.DefaultRequestHeaders.Remove("X-Test-Role");
        c.DefaultRequestHeaders.Add("X-Test-Role", "ADMIN");
        // The test factory installs a NoOp IAntiforgery so the *other* POST
        // tests can submit without juggling tokens. CSRF tests must opt-in
        // to real validation via this header (SecurityTests.cs:49 does the
        // same). Without it, the NoOp lets the request through, the action
        // body runs, fails ModelState (only Title posted), and renders the
        // form view — a misleading 200 OK that masked this test gap.
        c.DefaultRequestHeaders.Add(EsemsTestFactory.ValidateAntiforgeryHeader, "true");

        var r = await c.PostAsync(postPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Adversarial title",
        }));

        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        Assert.True(
            (int)r.StatusCode == 400
            || r.StatusCode == HttpStatusCode.Forbidden
            || r.StatusCode == HttpStatusCode.Redirect,
            $"POST {postPath} without CSRF token should be rejected; got {(int)r.StatusCode}.");
    }

    // TODO: Resource-level IDOR — once ownership-bound policies exist, write
    // a test that creates two records under different users and proves
    // /Resource/Edit/{otherUserId} rejects. Deferred — no infra today.
}
