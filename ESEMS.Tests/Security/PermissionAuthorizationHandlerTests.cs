using System.Security.Claims;
using ESEMS.Web.Security;
using Microsoft.AspNetCore.Authorization;

namespace ESEMS.Tests.Security;

/// <summary>
/// Branch-coverage tests for <see cref="PermissionAuthorizationHandler"/>.
/// Every pass/fail branch in the handler is exercised:
/// <list type="number">
///   <item>Unauthenticated principal → not granted</item>
///   <item>Exact Permission claim match → granted</item>
///   <item>Module wildcard (<c>Module.*</c>) claim → granted</item>
///   <item>Global wildcard (<c>*.*</c>) claim → granted</item>
///   <item>Non-matching permission claim → not granted</item>
///   <item>Empty claims → not granted</item>
///   <item>Legacy ClaimTypes.Role alone no longer grants anything</item>
/// </list>
/// </summary>
public class PermissionAuthorizationHandlerTests
{
    private static readonly PermissionAuthorizationHandler _handler = new();

    private static Task<AuthorizationHandlerContext> HandleAsync(
        ClaimsPrincipal principal, string requiredPermission)
    {
        var requirement = new PermissionRequirement(requiredPermission);
        var context = new AuthorizationHandlerContext(
            new[] { requirement }, principal, resource: null);
        return _handler.HandleAsync(context).ContinueWith(_ => context);
    }

    private static ClaimsPrincipal Anonymous()
        => new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated

    private static ClaimsPrincipal Authenticated(params Claim[] claims)
        => new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));

    // ---- Branch 1: unauthenticated ----

    [Fact]
    public async Task Anonymous_Denied()
    {
        var ctx = await HandleAsync(Anonymous(), "Improvement.Edit");
        Assert.False(ctx.HasSucceeded);
    }

    // ---- Branch: legacy ClaimTypes.Role alone is no longer load-bearing ----
    // Pre-rollout the handler had a short-circuit that treated the legacy
    // ADMIN role as equivalent to the *.* wildcard. After the CustomRole
    // removal that bypass was deleted — admins now reach this handler only
    // with a Permission claim sourced from the Administrator RoleGroup.
    // These tests pin the new behaviour so the bypass can't sneak back in.

    [Fact]
    public async Task LegacyAdminRoleClaim_Alone_Denied()
    {
        var user = Authenticated(new Claim(ClaimTypes.Role, "ADMIN"));
        var ctx = await HandleAsync(user, "Improvement.Edit");
        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task LegacyAdminRoleClaim_Alone_DeniedDangerousPermission()
    {
        var user = Authenticated(new Claim(ClaimTypes.Role, "ADMIN"));
        var ctx = await HandleAsync(user, "Users.Delete");
        Assert.False(ctx.HasSucceeded);
    }

    // ---- Exact permission claim ----

    [Fact]
    public async Task ExactPermissionClaim_Granted()
    {
        var user = Authenticated(new Claim("Permission", "Improvement.Edit"));
        var ctx = await HandleAsync(user, "Improvement.Edit");
        Assert.True(ctx.HasSucceeded);
    }

    // ---- Branch 4: module wildcard ----

    [Fact]
    public async Task ModuleWildcard_GrantsEveryActionInModule()
    {
        var user = Authenticated(new Claim("Permission", "Improvement.*"));
        var ctx = await HandleAsync(user, "Improvement.Edit");
        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task ModuleWildcard_DoesNotGrantOtherModules()
    {
        var user = Authenticated(new Claim("Permission", "Improvement.*"));
        var ctx = await HandleAsync(user, "Risk.Edit");
        Assert.False(ctx.HasSucceeded);
    }

    // ---- Branch 5: global wildcard ----

    [Fact]
    public async Task GlobalWildcard_GrantsAnyPermission()
    {
        var user = Authenticated(new Claim("Permission", "*.*"));
        var ctx = await HandleAsync(user, "Users.Delete");
        Assert.True(ctx.HasSucceeded);
    }

    // ---- Branch 6: non-matching permission claim ----

    [Fact]
    public async Task NonMatching_Denied()
    {
        var user = Authenticated(new Claim("Permission", "Improvement.View"));
        var ctx = await HandleAsync(user, "Improvement.Edit");
        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task WrongModuleExact_Denied()
    {
        var user = Authenticated(new Claim("Permission", "Risk.Edit"));
        var ctx = await HandleAsync(user, "Improvement.Edit");
        Assert.False(ctx.HasSucceeded);
    }

    // ---- Branch 7: empty claim set (authenticated but nothing else) ----

    [Fact]
    public async Task AuthenticatedButNoPermissions_Denied()
    {
        var user = Authenticated(); // No role, no Permission claims
        var ctx = await HandleAsync(user, "Improvement.Edit");
        Assert.False(ctx.HasSucceeded);
    }

    // ---- Permissions accumulate (multiple claims) ----

    [Fact]
    public async Task MultiplePermissionClaims_OrLogic()
    {
        var user = Authenticated(
            new Claim("Permission", "Process.View"),
            new Claim("Permission", "Improvement.Edit"), // the one we need
            new Claim("Permission", "Risk.View"));
        var ctx = await HandleAsync(user, "Improvement.Edit");
        Assert.True(ctx.HasSucceeded);
    }

    // ---- Constructor validation ----

    [Fact]
    public void PermissionRequirement_RejectsNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() => new PermissionRequirement(""));
        Assert.Throws<ArgumentException>(() => new PermissionRequirement("   "));
        Assert.Throws<ArgumentException>(() => new PermissionRequirement(null!));
    }
}
