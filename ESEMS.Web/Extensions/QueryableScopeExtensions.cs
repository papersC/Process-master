using ESEMS.Web.Models.Common;
using ESEMS.Web.Services.Common;

namespace ESEMS.Web.Extensions;

/// <summary>
/// IQueryable extension methods that append a WHERE clause to filter entities
/// by the logged-in user's data-visibility scope. Each method is a no-op
/// when <see cref="ScopeContext.IsUnscoped"/> (ScopeLevel = All).
///
/// Three overloads handle the three FK naming patterns across ESEMS entities:
/// <list type="bullet">
/// <item><see cref="ApplyOwningUnitScope{T}"/> — <c>OwningUnitId</c> (Process, Service, Improvement, ChangeRequest)</item>
/// <item><see cref="ApplyAssignedUnitScope{T}"/> — <c>AssignedToUnitId</c> (Asset, Incident, Problem)</item>
/// <item><see cref="ApplyOrganizationScope{T}"/> — <c>OrganizationUnitId</c> (EnterpriseRisk)</item>
/// </list>
/// </summary>
public static class QueryableScopeExtensions
{
    /// <summary>
    /// Filters entities where <c>OwningUnitId</c> is in the user's visible units.
    /// Entities with a null OwningUnitId are included (they're unassigned → visible to everyone).
    /// </summary>
    public static IQueryable<T> ApplyOwningUnitScope<T>(this IQueryable<T> query, ScopeContext scope)
        where T : class, IOwnedByUnit
    {
        if (scope.IsUnscoped || scope.VisibleUnitIds == null)
            return query;

        return query.Where(x => x.OwningUnitId == null || scope.VisibleUnitIds.Contains(x.OwningUnitId.Value));
    }

    /// <summary>
    /// Filters entities where <c>AssignedToUnitId</c> is in the user's visible units.
    /// </summary>
    public static IQueryable<T> ApplyAssignedUnitScope<T>(this IQueryable<T> query, ScopeContext scope)
        where T : class, IAssignedToUnit
    {
        if (scope.IsUnscoped || scope.VisibleUnitIds == null)
            return query;

        return query.Where(x => x.AssignedToUnitId == null || scope.VisibleUnitIds.Contains(x.AssignedToUnitId.Value));
    }

    /// <summary>
    /// Filters entities where <c>OrganizationUnitId</c> is in the user's visible units.
    /// </summary>
    public static IQueryable<T> ApplyOrganizationScope<T>(this IQueryable<T> query, ScopeContext scope)
        where T : class, IOrganizationScoped
    {
        if (scope.IsUnscoped || scope.VisibleUnitIds == null)
            return query;

        return query.Where(x => x.OrganizationUnitId == null || scope.VisibleUnitIds.Contains(x.OrganizationUnitId.Value));
    }
}
