using System.Net;
using System.Net.Http;

namespace ESEMS.SecurityTests;

/// <summary>
/// Tier-3 live-server security smoke. Hits a RUNNING ESEMS instance
/// (default http://localhost:5297, override via ESEMS_E2E_BASE_URL) so we
/// see what the production middleware pipeline actually emits — distinct
/// from ESEMS.Tests/Integration/SecurityTests.cs which hits the
/// WebApplicationFactory (test-flavored DI, no-op IAntiforgery).
///
/// Skips gracefully if the server isn't reachable on /health.
///
/// What it proves:
///   * SecurityHeadersMiddleware emits CSP + the OWASP secure-defaults set
///   * Auth-cookie carries HttpOnly + SameSite (Secure depends on HTTPS, see note)
///   * Protected GETs redirect anonymous traffic to /Account/Login
///   * Login surface is still publicly reachable
/// </summary>
public class LiveServerSecuritySmoke
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("ESEMS_E2E_BASE_URL")?.TrimEnd('/')
        ?? "http://localhost:5297";

    private static HttpClient NewClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        return new HttpClient(handler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(10) };
    }

    private static async Task<bool> IsReachableAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var r = await http.GetAsync($"{BaseUrl}/health");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ─────────────────────────────────────────────────────────────────────
    // OWASP secure-defaults headers — split into one assertion per concern
    // so each finding is isolated in the test output.
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginPage_HasXContentTypeOptionsNosniff()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        Assert.True(r.Headers.TryGetValues("X-Content-Type-Options", out var xcto) &&
                    string.Equals(string.Join(",", xcto), "nosniff", StringComparison.OrdinalIgnoreCase),
            "X-Content-Type-Options must be 'nosniff'.");
    }

    [Fact]
    public async Task LoginPage_HasClickjackingProtection()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        var xfo = r.Headers.TryGetValues("X-Frame-Options", out var v) ? string.Join(",", v) : null;
        var csp = r.Headers.TryGetValues("Content-Security-Policy", out var c) ? string.Join("|", c) : null;
        var frameBusted =
            (xfo is not null && (xfo.Contains("DENY", StringComparison.OrdinalIgnoreCase)
                              || xfo.Contains("SAMEORIGIN", StringComparison.OrdinalIgnoreCase)))
            || (csp is not null && csp.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase));
        Assert.True(frameBusted, $"No clickjacking protection. X-Frame-Options='{xfo}', CSP='{csp}'.");
    }

    [Fact]
    public async Task LoginPage_HasReferrerPolicy()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        Assert.True(r.Headers.Contains("Referrer-Policy"),
            "Referrer-Policy header missing.");
    }

    [Fact]
    public async Task CSP_ScriptSrc_DoesNot_Allow_UnsafeInline()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        var csp = r.Headers.TryGetValues("Content-Security-Policy", out var c) ? string.Join("|", c) : null;
        Assert.NotNull(csp);
        var scriptSrc = csp!
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(d => d.StartsWith("script-src", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(scriptSrc);
        Assert.DoesNotContain("'unsafe-inline'", scriptSrc);
    }

    /// <summary>
    /// M-7 (Tier-3 — hotfix reverted): the high-risk CSS-injection vector is
    /// <c>&lt;style&gt;</c> blocks. CSP3's <c>style-src-elem</c> would block
    /// inline <c>&lt;style&gt;</c> when locked down to nonce-only — but we
    /// learned the hard way that Razor partials in ESEMS emit 8+ <c>&lt;style&gt;</c>
    /// blocks per page without a nonce (e.g. _Layout's .page-header-logo
    /// size constraint), and tightening style-src-elem broke the layout.
    /// Until those blocks are migrated to carry <c>nonce="@@Context.CspNonce()"</c>,
    /// style-src-elem keeps <c>'unsafe-inline'</c>. This test currently just
    /// asserts the directive exists; once nonce migration is done, swap to
    /// <c>Assert.DoesNotContain("'unsafe-inline'", styleSrcElem)</c>.
    /// </summary>
    [Fact]
    public async Task CSP_StyleSrcElem_IsConfigured()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        var csp = r.Headers.TryGetValues("Content-Security-Policy", out var c) ? string.Join("|", c) : null;
        Assert.NotNull(csp);
        var styleSrcElem = csp!
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(d => d.StartsWith("style-src-elem", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(styleSrcElem);
        // 'unsafe-inline' must be present (until nonce-migration is done).
        // No nonce on style-src-elem on purpose — CSP-3 says nonce + unsafe-
        // inline in the same directive forces nonce-only, which broke layouts.
        Assert.Contains("'unsafe-inline'", styleSrcElem);
    }

    /// <summary>
    /// M-9 (Tier-3): the script-src directive must not whitelist any CDN
    /// origins. Every JS dependency is self-hosted under wwwroot/lib/, so
    /// the CSP shouldn't quietly authorize cdn.jsdelivr.net / unpkg.com /
    /// etc. (defense-in-depth: a compromised CDN can't serve nonce-less
    /// attacker JS through a permitted origin).
    /// </summary>
    [Fact]
    public async Task CSP_ScriptSrc_DoesNot_Allow_ThirdPartyCDNs()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        var csp = r.Headers.TryGetValues("Content-Security-Policy", out var c) ? string.Join("|", c) : null;
        Assert.NotNull(csp);
        var scriptSrc = csp!
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(d => d.StartsWith("script-src", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(scriptSrc);
        foreach (var cdn in new[] { "cdn.jsdelivr.net", "cdnjs.cloudflare.com", "unpkg.com", "cdn.datatables.net", "cdn.tailwindcss.com" })
        {
            Assert.DoesNotContain(cdn, scriptSrc);
        }
    }

    /// <summary>
    /// M-3 (OWASP 2024+): X-XSS-Protection: 1; mode=block can introduce
    /// side-channel XSS in older browsers. Modern guidance: omit or set 0.
    /// </summary>
    [Fact]
    public async Task XXssProtection_NotEnabled()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        if (r.Headers.TryGetValues("X-XSS-Protection", out var v))
        {
            var header = string.Join(",", v).Trim();
            Assert.True(header == "0",
                $"X-XSS-Protection should be omitted or '0'; got '{header}' (legacy 'mode=block' is a side-channel risk).");
        }
    }

    /// <summary>
    /// CSP should pin frame-ancestors, base-uri, and form-action — three
    /// defenses-in-depth that XFO alone doesn't cover.
    /// </summary>
    [Fact]
    public async Task CSP_PinsFrameAncestors_BaseUri_FormAction()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        var csp = r.Headers.TryGetValues("Content-Security-Policy", out var c) ? string.Join("|", c) : null;
        Assert.NotNull(csp);
        Assert.Contains("frame-ancestors", csp);
        Assert.Contains("base-uri", csp);
        Assert.Contains("form-action", csp);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Auth cookie hardening — HttpOnly + SameSite present.
    // (Secure flag isn't asserted because the dev server is plain HTTP;
    //  in production behind ejaar360.com the kestrel/IIS HTTPS layer adds it.)
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task LoginRequest_GetsCsrfCookie_WithHttpOnlyAndSameSite()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        Assert.True(r.Headers.TryGetValues("Set-Cookie", out var cookies),
            "Expected at least one Set-Cookie header from /Account/Login.");
        var joined = string.Join(" ;; ", cookies);
        Assert.Contains("HttpOnly", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=", joined, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Protected routes redirect anonymous traffic.
    // ─────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("/Processes")]
    [InlineData("/Services")]
    [InlineData("/Assets")]
    [InlineData("/EnterpriseRisks")]
    [InlineData("/SettingsHub")]
    [InlineData("/Users")]
    [InlineData("/AuditLogs")]
    [InlineData("/CustomerFeedback")]
    public async Task Protected_Get_RedirectsAnonymousToLogin(string path)
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync(path);
        Assert.True(
            r.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized,
            $"GET {path} should redirect/401 anonymous; got {(int)r.StatusCode}.");
        if (r.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found)
        {
            Assert.Contains("/Account/Login", r.Headers.Location?.OriginalString ?? string.Empty);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Login surface MUST stay anonymous-reachable.
    // ─────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/health")]
    public async Task LoginSurface_StaysPublic(string path)
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Server fingerprint is suppressed (no leaky "Server: Microsoft-IIS/X").
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Response_DoesNot_Leak_ServerSoftwareHeader()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var r = await client.GetAsync("/Account/Login");
        if (r.Headers.TryGetValues("Server", out var v))
        {
            var server = string.Join(",", v);
            Assert.False(
                server.Contains("Microsoft-IIS", StringComparison.OrdinalIgnoreCase)
                || server.Contains("Kestrel", StringComparison.OrdinalIgnoreCase),
                $"Server header leaks platform identity: '{server}'. Strip via SecurityHeadersMiddleware.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Adversarial: POST without antiforgery to a protected action.
    // (Login endpoint specifically — it rate-limits by username, so
    //  a tokenless attacker must be blocked BEFORE burning lockout budget.)
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task LoginPost_WithoutAntiforgery_IsRejected()
    {
        if (!await IsReachableAsync()) return;
        using var client = NewClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "admin",
            ["Password"] = "wrong-on-purpose",
        });
        var r = await client.PostAsync("/Account/Login", form);
        Assert.True(
            r.StatusCode == HttpStatusCode.BadRequest
            || r.StatusCode == HttpStatusCode.Forbidden,
            $"Tokenless POST to /Account/Login should reject at the CSRF gate; got {(int)r.StatusCode}.");
    }
}
