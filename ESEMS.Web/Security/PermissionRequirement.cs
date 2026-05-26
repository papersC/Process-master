using Microsoft.AspNetCore.Authorization;

namespace ESEMS.Web.Security;

/// <summary>
/// Authorization requirement that expects the current user to hold a
/// <c>Permission</c> claim matching a given <c>Module.Action</c> key
/// (e.g. <c>Improvement.Edit</c>).
///
/// Part of Plan X — the matrix-driven authorization bridge. Permissions are
/// OR-ed: a user who holds the exact claim OR a wildcard <c>Module.*</c>
/// OR the global <c>*.*</c> administrator claim passes.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("Permission key required.", nameof(permission));
        Permission = permission;
    }
}
