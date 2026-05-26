using ESEMS.Web.Models.Common;

namespace ESEMS.Web.Services.Common;

/// <summary>
/// Per-record IDOR guard helpers that complement the list-filtering use of
/// <see cref="IScopingService"/>. List queries already filter by scope; these
/// helpers cover the missing case: a user navigating directly to
/// <c>/Resource/Details/{id}</c> by URL tampering.
///
/// Usage in a controller action:
///
///     var risk = await _context.EnterpriseRisks.FirstOrDefaultAsync(r => r.Id == id);
///     if (risk == null) return NotFound();
///     var scope = await _scopingService.GetScopeAsync(User);
///     if (!scope.CanAccess(risk)) return NotFound();   // 404 not 403 — don't leak existence
///     ...
///
/// Returning <c>NotFound()</c> rather than <c>Forbid()</c> is deliberate:
/// it doesn't leak whether the record exists. A 403 would tell the attacker
/// "yes, this id is real, you just can't see it".
/// </summary>
public static class ScopeContextExtensions
{
    /// <summary>
    /// Unscoped users (ScopeLevel=All) see everything. Scoped users see only
    /// records whose <see cref="IOrganizationScoped.OrganizationUnitId"/> is
    /// in their <see cref="ScopeContext.VisibleUnitIds"/> set. Records with
    /// a null <c>OrganizationUnitId</c> are visible to everyone (orphans).
    /// </summary>
    public static bool CanAccess(this ScopeContext scope, IOrganizationScoped entity)
    {
        if (scope.IsUnscoped) return true;
        if (entity.OrganizationUnitId == null) return true;
        return scope.VisibleUnitIds?.Contains(entity.OrganizationUnitId.Value) ?? false;
    }

    /// <summary>
    /// Same contract for entities with <c>OwningUnitId</c>
    /// (Process, Service, Improvement, ChangeRequest).
    /// </summary>
    public static bool CanAccess(this ScopeContext scope, IOwnedByUnit entity)
    {
        if (scope.IsUnscoped) return true;
        if (entity.OwningUnitId == null) return true;
        return scope.VisibleUnitIds?.Contains(entity.OwningUnitId.Value) ?? false;
    }

    /// <summary>
    /// Same contract for entities with <c>AssignedToUnitId</c>
    /// (Asset, Incident, Problem).
    /// </summary>
    public static bool CanAccess(this ScopeContext scope, IAssignedToUnit entity)
    {
        if (scope.IsUnscoped) return true;
        if (entity.AssignedToUnitId == null) return true;
        return scope.VisibleUnitIds?.Contains(entity.AssignedToUnitId.Value) ?? false;
    }
}
