using Microsoft.AspNetCore.Authorization;

namespace ESEMS.Web.Security;

/// <summary>
/// Resolves <see cref="PermissionRequirement"/> against the
/// <c>Permission</c> claims on the current principal. Claims are emitted
/// at login by <c>AccountController.BuildSignInPrincipalAsync</c> from the
/// user's <c>UserRoleGroup</c> assignments.
///
/// Matching rules:
/// 1. Exact match on <c>Module.Action</c> → grant
/// 2. Wildcard module match <c>Module.*</c> → grant
/// 3. Global administrator wildcard <c>*.*</c> → grant
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        var required = requirement.Permission;
        var dot = required.IndexOf('.');
        var module = dot > 0 ? required[..dot] : required;
        var wildcard = $"{module}.*";

        var permissionClaims = context.User.FindAll("Permission");
        foreach (var claim in permissionClaims)
        {
            var value = claim.Value;
            if (value == required || value == wildcard || value == "*.*")
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}
