using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ESEMS.Tests.Integration;

public class SecurityTests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public SecurityTests(EsemsTestFactory factory) { _factory = factory; }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Every protected POST in the app carries [ValidateAntiForgeryToken]. A
        // request that omits the token must NOT 200, regardless of auth.
        var r = await client.PostAsync("/Workflow/Delegate",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["workflowId"] = "anything",
                ["targetUserId"] = "1"
            }));

        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        Assert.True(r.StatusCode == HttpStatusCode.BadRequest
                 || r.StatusCode == HttpStatusCode.Forbidden
                 || r.StatusCode == HttpStatusCode.Redirect
                 || r.StatusCode == HttpStatusCode.Unauthorized
                 || (int)r.StatusCode == 400,
            $"Expected rejection but got {(int)r.StatusCode} {r.StatusCode}");
    }

    [Fact]
    public async Task LoginPost_WithoutAntiforgeryToken_IsRejected()
    {
        // Regression test for UX/QA Batch 5 verification: brute-force scripts
        // hitting /Account/Login without first scraping the antiforgery
        // token must be rejected at the CSRF gate, BEFORE the rate-limiter
        // (which is keyed by username). This is defense-in-depth — a token-
        // less attacker never reaches the rate-limiter, never burns lockout
        // budget against a real user.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "ANON");
        // The test factory's no-op IAntiforgery is opt-out; flip it back to
        // real-validation mode for this specific test so we can verify the
        // production CSRF behaviour.
        client.DefaultRequestHeaders.Add(EsemsTestFactory.ValidateAntiforgeryHeader, "true");

        var r = await client.PostAsync("/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Username"] = "admin",
                ["Password"] = "anything",
            }));

        Assert.True(
            r.StatusCode == HttpStatusCode.BadRequest
            || r.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 400/403 (CSRF rejection) on token-less login POST; got {(int)r.StatusCode}.");
    }

    [Fact]
    public async Task SecurityHeaders_AreSet_OnLoginPage()
    {
        // Headers verified via live probe in UX/QA Batch 5. Locking the
        // contract here so a layout/middleware regression fails the build.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "ANON");
        var r = await client.GetAsync("/Account/Login");

        Assert.True(r.Headers.TryGetValues("X-Content-Type-Options", out var xcto));
        Assert.Contains("nosniff", xcto!);

        Assert.True(r.Headers.TryGetValues("Content-Security-Policy", out var csp));
        var cspValue = string.Join(';', csp!);
        Assert.Contains("default-src 'self'", cspValue);

        Assert.True(r.Headers.TryGetValues("Referrer-Policy", out _));
        Assert.True(r.Headers.TryGetValues("Permissions-Policy", out _));
        Assert.True(r.Headers.TryGetValues("X-Frame-Options", out _));
    }

    [Fact]
    public async Task AnonymousUser_Cannot_Access_SettingsHub()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "ANON");

        var r = await client.GetAsync("/SettingsHub");

        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task UserWithoutPermission_Gets403_OnProtectedRoute()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        // Authed user, but no permissions claim — the Plan-X policy check should reject.
        client.DefaultRequestHeaders.Add("X-Test-Role", "VIEWER");
        client.DefaultRequestHeaders.Add("X-Test-Perms", "Improvement.View"); // NO Improvement.Edit

        var r = await client.GetAsync("/Improvements/Wizard");

        // Wizard is gated on Improvement.Create — our principal only carries
        // Improvement.View, so authorization must reject.
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task UserWithExactPermission_CanAccess_ProtectedRoute()
    {
        var client = _factory.CreateClient();
        // Give only what's needed — proves the Plan-X matcher reads the single permission.
        client.DefaultRequestHeaders.Add("X-Test-Perms", "Improvement.View");

        var r = await client.GetAsync("/Improvements");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task SecurityHeaders_AreSet_OnAuthenticatedResponse()
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync("/Categories");

        Assert.True(r.Headers.TryGetValues("X-Content-Type-Options", out var xcto));
        Assert.Contains("nosniff", xcto!);

        Assert.True(r.Headers.TryGetValues("X-Frame-Options", out _)
                 || r.Headers.Contains("Content-Security-Policy"),
            "Expected clickjacking protection (X-Frame-Options or CSP frame-ancestors)");
    }

    [Fact]
    public async Task WildcardPermission_Grants_AllRoutes()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Perms", "*.*");

        // Every granular route under * should pass.
        foreach (var path in new[] { "/Categories", "/SLA", "/Users", "/Workflow/PendingApprovals", "/SettingsHub" })
        {
            var r = await client.GetAsync(path);
            Assert.True(r.StatusCode == HttpStatusCode.OK,
                $"Wildcard permission should grant {path} but got {(int)r.StatusCode}");
        }
    }
}
