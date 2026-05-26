using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Test-only auth handler. Reads identity from request headers so tests can
/// dictate "who" makes the call:
///
///   X-Test-UserId      — numeric id for ApproverUserId lookups (default 1).
///   X-Test-Name        — display name (default "Test Admin").
///   X-Test-Role        — legacy role claim (default "ADMIN"). Use "ANON" to skip auth.
///   X-Test-Perms       — comma-separated Plan-X policy names; default "*.*" matches everything.
///   X-Test-ScopeLevel  — data-visibility scope ("OwningUnit"/"Process"); omitted ⇒ "All" (unscoped).
///   X-Test-OrgUnitId   — the caller's org unit id; required for a scoped (non-All) identity.
///
/// The last two let an integration test forge a *scoped* caller so the
/// per-record IDOR guards (ScopingService.GetScopeAsync reads exactly these two
/// claims) can be exercised — see Regression_2026_05_23_Tests.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (string.Equals(role, "ANON", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = Request.Headers["X-Test-UserId"].ToString();
        if (string.IsNullOrEmpty(userId)) userId = "1";
        var name = Request.Headers["X-Test-Name"].ToString();
        if (string.IsNullOrEmpty(name)) name = "Test Admin";
        if (string.IsNullOrEmpty(role)) role = "ADMIN";

        var permsHeader = Request.Headers["X-Test-Perms"].ToString();
        var perms = string.IsNullOrEmpty(permsHeader)
            ? new[] { "*.*" }
            : permsHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new("UserId", userId),
        };
        foreach (var p in perms) claims.Add(new Claim("Permission", p));

        // Data-visibility scope claims (read by ScopingService.GetScopeAsync).
        // Omit X-Test-ScopeLevel ⇒ no claim ⇒ ScopeLevel defaults to "All"
        // (unscoped), preserving every existing test's behaviour.
        var scopeLevel = Request.Headers["X-Test-ScopeLevel"].ToString();
        if (!string.IsNullOrEmpty(scopeLevel))
            claims.Add(new Claim("ScopeLevel", scopeLevel));
        var orgUnitId = Request.Headers["X-Test-OrgUnitId"].ToString();
        if (!string.IsNullOrEmpty(orgUnitId))
            claims.Add(new Claim("OrganizationUnitId", orgUnitId));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
