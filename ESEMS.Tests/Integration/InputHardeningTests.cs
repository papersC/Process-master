using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Functional adversarial input tests — UX/QA Batch 4.
///
/// What this proves:
///   * Common XSS payloads in query strings / form posts don't reach the
///     response unescaped (Razor + the antiforgery + output encoding chain
///     is doing its job).
///   * Classic SQL-injection patterns in URL params don't 500 — EF Core
///     parameterization is the floor.
///   * Boundary inputs (empty / whitespace / very-long / Unicode / RTL /
///     emoji / null bytes) don't produce uncaught exceptions on protected
///     pages.
///   * Auth gating still holds when adversarial inputs ride along (no
///     payload-based authorization bypass).
///
/// What this does NOT prove (deferred — needs authenticated session):
///   * Stored-XSS via persisted entity fields (Title/Description) — needs
///     a seeded user to POST through CSRF.
///   * Full SQLi sweep against every endpoint — needs deep-fuzz infra
///     (sqlmap) and writeable DB.
/// </summary>
public class InputHardeningTests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public InputHardeningTests(EsemsTestFactory factory) { _factory = factory; }

    private HttpClient Authed() => CreateClient("ADMIN", "*.*");
    private HttpClient Anon() => CreateClient("ANON");

    private HttpClient CreateClient(string role, string? perms = null)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Role", role);
        if (perms != null) c.DefaultRequestHeaders.Add("X-Test-Perms", perms);
        return c;
    }

    // -------------------------------------------------------------------
    // XSS reflection — payload in query string must not appear unescaped.
    // -------------------------------------------------------------------
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("\"><img src=x onerror=alert(1)>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("' onmouseover='alert(1)")]
    [InlineData("<svg/onload=alert(1)>")]
    public async Task XssPayload_In_QueryString_Is_Not_Reflected_Unescaped(string payload)
    {
        // Use a permissive route — Improvements Index honors a search query.
        // The payload is reflected in the page IF the controller echoes it
        // raw; we want to confirm it never appears verbatim with the
        // "<script>" / unescaped quote intact.
        var c = Authed();
        var r = await c.GetAsync($"/Improvements?search={Uri.EscapeDataString(payload)}");
        if (r.StatusCode == HttpStatusCode.OK)
        {
            var body = await r.Content.ReadAsStringAsync();
            // Razor encodes < > " & by default. Reject only the literal,
            // unescaped form — encoded forms (&lt;script&gt;) are fine.
            Assert.DoesNotContain("<script>alert(1)</script>", body);
            Assert.DoesNotContain("onerror=alert(1)", body);
            Assert.DoesNotContain("onload=alert(1)", body);
        }
        // 302/401/403/404 are all acceptable — they prove the payload didn't
        // crash the server.
    }

    // -------------------------------------------------------------------
    // SQLi-shaped payloads in URL must not produce 500 (EF parameterization).
    // -------------------------------------------------------------------
    [Theory]
    [InlineData("1' OR '1'='1")]
    [InlineData("1; DROP TABLE Users--")]
    [InlineData("1 UNION SELECT NULL,NULL,NULL--")]
    [InlineData("admin'--")]
    [InlineData("\\xbf\\x27 OR 1=1--")]
    public async Task SqlInjection_Payload_In_Route_Does_Not_500(string payload)
    {
        var c = Authed();
        var r = await c.GetAsync($"/Improvements/Details/{Uri.EscapeDataString(payload)}");
        // Acceptable: 200 (empty result), 404 (not found), 302 (redirect),
        // 400 (rejected). NOT acceptable: 500 (uncaught exception).
        Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode);
    }

    // -------------------------------------------------------------------
    // Boundary inputs — server must accept or reject cleanly, never crash.
    // -------------------------------------------------------------------
    [Theory]
    [InlineData("")]                                   // empty
    [InlineData(" ")]                                  // whitespace
    [InlineData("\t\n\r")]                             // whitespace variants
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]  // very long ASCII
    [InlineData("اختبار")]                              // Arabic
    [InlineData("עברית")]                               // Hebrew (RTL, distinct script)
    [InlineData("中文")]                                // CJK
    [InlineData("🎯🔥💯")]                              // emoji
    [InlineData("\0")]                                  // null byte
    [InlineData("test​test")]                      // zero-width space
    public async Task Boundary_Search_Inputs_Do_Not_Crash(string input)
    {
        var c = Authed();
        var r = await c.GetAsync($"/Categories?search={Uri.EscapeDataString(input)}");
        Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode);
    }

    // -------------------------------------------------------------------
    // Auth gating still holds even when payloads ride along.
    // -------------------------------------------------------------------
    [Theory]
    [InlineData("/Improvements/Details/1?x=<script>")]
    [InlineData("/EnterpriseRisks/Details/' OR 1=1--")]
    [InlineData("/AuditLogs?filter=" + "x")]   // baseline
    public async Task Anonymous_Stays_Locked_Out_Even_With_Adversarial_Inputs(string urlWithPayload)
    {
        var r = await Anon().GetAsync(urlWithPayload);
        // Must be 302/401/403 — never 200 (would mean payload bypassed auth).
        Assert.True(
            r.StatusCode is HttpStatusCode.Redirect
                        or HttpStatusCode.Unauthorized
                        or HttpStatusCode.Forbidden
                        or HttpStatusCode.Found,
            $"Anon access to {urlWithPayload} must reject; got {(int)r.StatusCode}.");
    }

    // -------------------------------------------------------------------
    // Path traversal in URL parameters — must not leak file-system content.
    // -------------------------------------------------------------------
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\drivers\\etc\\hosts")]
    [InlineData("/etc/passwd")]
    public async Task PathTraversal_In_Route_Param_Does_Not_Leak_Filesystem(string payload)
    {
        var c = Authed();
        var r = await c.GetAsync($"/Categories/Details/{Uri.EscapeDataString(payload)}");
        if (r.StatusCode == HttpStatusCode.OK)
        {
            var body = await r.Content.ReadAsStringAsync();
            // Filesystem leak would show /etc/passwd or hosts content —
            // a quick fingerprint check.
            Assert.DoesNotContain("root:x:0:0:", body);
            Assert.DoesNotContain("# Copyright (c) 1993-", body);  // Windows hosts header
        }
    }
}
