using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ESEMS.Web.Data;

namespace ESEMS.Web.Services.Common;

/// <summary>
/// Resolves the logged-in user's data-visibility scope from their claims.
///
/// The common case (<c>ScopeLevel = All</c>) returns immediately with no DB
/// hit. For scoped users, the APQC org-unit tree is loaded once and cached
/// in <see cref="IMemoryCache"/> (the tree is typically &lt;100 nodes at
/// MBRHE, so holding it in memory is cheap).
/// </summary>
public sealed class ScopingService : IScopingService
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private const string OrgTreeCacheKey = "ScopingService:OrgTree";
    private static readonly TimeSpan OrgTreeCacheDuration = TimeSpan.FromMinutes(30);

    public ScopingService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<ScopeContext> GetScopeAsync(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return ScopeContext.Unscoped;

        var scopeLevel = user.FindFirst("ScopeLevel")?.Value ?? "All";
        if (scopeLevel == "All")
            return ScopeContext.Unscoped;

        var orgUnitIdRaw = user.FindFirst("OrganizationUnitId")?.Value;
        if (string.IsNullOrWhiteSpace(orgUnitIdRaw) || !int.TryParse(orgUnitIdRaw, out var orgUnitId))
        {
            // SEC-012: the user is scoped (ScopeLevel != All) but we couldn't
            // resolve their org unit (CustomUser.UnitId didn't map to an APQC
            // OrganizationUnit at login). FAIL CLOSED: give them an EMPTY
            // visible-unit set so list queries surface no unit-assigned records,
            // instead of the previous fail-OPEN behavior that fell back to
            // Unscoped and silently exposed every unit's data. A misconfigured
            // scoped account now sees (almost) nothing rather than everything;
            // the fix is to map their unit, not to widen their scope.
            // ScopeLevel == "All" is handled above and never reaches here.
            return new ScopeContext
            {
                ScopeLevel = scopeLevel,
                VisibleUnitIds = new HashSet<int>(),
                VisibleProcessIds = new HashSet<string>(StringComparer.Ordinal)
            };
        }

        // Load the org tree (cached). Each node is just (Id, ParentId).
        var tree = await GetOrgTreeAsync();

        // Walk downward from the user's unit to collect all child IDs.
        var visibleUnitIds = new HashSet<int> { orgUnitId };
        var queue = new Queue<int>();
        queue.Enqueue(orgUnitId);
        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (tree.TryGetValue(parentId, out var children))
            {
                foreach (var childId in children)
                {
                    if (visibleUnitIds.Add(childId))
                        queue.Enqueue(childId);
                }
            }
        }

        if (scopeLevel == "Process")
        {
            // For Process scope: find all processes owned by the visible
            // units, then return those process IDs for entity filtering.
            var processIds = await _db.Processes
                .Where(p => !p.IsDeleted && p.OwningUnitId != null
                         && visibleUnitIds.Contains(p.OwningUnitId.Value))
                .Select(p => p.Id)
                .ToListAsync();

            return new ScopeContext
            {
                ScopeLevel = "Process",
                VisibleUnitIds = visibleUnitIds,
                VisibleProcessIds = new HashSet<string>(processIds, StringComparer.Ordinal)
            };
        }

        return new ScopeContext
        {
            ScopeLevel = "OwningUnit",
            VisibleUnitIds = visibleUnitIds
        };
    }

    /// <summary>
    /// Loads the APQC OrganizationUnit tree as a parent→children lookup.
    /// Cached for 30 minutes since the org structure changes rarely.
    /// </summary>
    private async Task<Dictionary<int, List<int>>> GetOrgTreeAsync()
    {
        if (_cache.TryGetValue(OrgTreeCacheKey, out Dictionary<int, List<int>>? cached) && cached != null)
            return cached;

        var units = await _db.OrganizationUnits
            .Where(ou => !ou.IsDeleted && ou.IsActive)
            .Select(ou => new { ou.Id, ou.ParentId })
            .AsNoTracking()
            .ToListAsync();

        var tree = new Dictionary<int, List<int>>();
        foreach (var u in units)
        {
            if (u.ParentId == null) continue;
            if (!tree.TryGetValue(u.ParentId.Value, out var list))
            {
                list = new List<int>();
                tree[u.ParentId.Value] = list;
            }
            list.Add(u.Id);
        }

        _cache.Set(OrgTreeCacheKey, tree, OrgTreeCacheDuration);
        return tree;
    }
}
