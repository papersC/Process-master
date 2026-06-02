using System.Security.Claims;

namespace ESEMS.Web.Helpers;

/// <summary>
/// View-side permission helpers. Lets Razor views gate action UI (buttons,
/// links, menu items) using the SAME matching rules the server-side
/// authorization uses, so what the user can see matches what the
/// <c>[Authorize(Policy = "Module.Action")]</c> attributes will actually allow.
///
/// RBAC-002 (QA 2026-06-02): several Details views gated Edit/Link controls on
/// <c>User.HasClaim("Permission", "CanEdit")</c>. "CanEdit" is a POLICY name
/// (<see cref="ESEMS.Web.Security.AppPolicies.CanEdit"/>), never a Permission
/// CLAIM value — claim values are <c>Module.Action</c> (e.g. "Service.Edit") or
/// the wildcards. That check was therefore always false for everyone, including
/// administrators, so those controls were invisible to all users. Use
/// <see cref="HasPermission"/> instead.
/// </summary>
public static class PermissionViewExtensions
{
    /// <summary>
    /// True when the principal holds a <c>Permission</c> claim satisfying the
    /// given <c>Module.Action</c> key. Mirrors
    /// <c>PermissionAuthorizationHandler</c>:
    ///   exact "Module.Action"  OR  module wildcard "Module.*"  OR  global "*.*".
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal? user, string moduleAction)
    {
        if (user is null || string.IsNullOrEmpty(moduleAction))
            return false;

        var dot = moduleAction.IndexOf('.');
        var moduleWildcard = dot > 0
            ? string.Concat(moduleAction.AsSpan(0, dot), ".*")
            : null;

        foreach (var claim in user.FindAll("Permission"))
        {
            var value = claim.Value;
            if (value == "*.*"
                || value == moduleAction
                || (moduleWildcard != null && value == moduleWildcard))
                return true;
        }
        return false;
    }
}
