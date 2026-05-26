namespace ESEMS.Web.Services.Common;

/// <summary>
/// Captures the data-visibility scope for the current request. Built once
/// per request by <see cref="IScopingService.GetScopeAsync"/> from the
/// user's claims, then passed to <c>QueryableScopeExtensions</c> to filter
/// list queries.
///
/// <list type="bullet">
/// <item><c>ScopeLevel = "All"</c> → no filtering; user sees everything.</item>
/// <item><c>ScopeLevel = "OwningUnit"</c> → filter by <see cref="VisibleUnitIds"/>
///   (the user's org unit + all child units in the tree).</item>
/// <item><c>ScopeLevel = "Process"</c> → filter by <see cref="VisibleProcessIds"/>
///   (processes owned by the user's scoped units).</item>
/// </list>
/// </summary>
public sealed class ScopeContext
{
    public string ScopeLevel { get; init; } = "All";

    /// <summary>
    /// APQC OrganizationUnit IDs the user can see. Null when <see cref="IsUnscoped"/>.
    /// Populated by walking the org tree downward from the user's unit.
    /// (int now that OrganizationUnit.Id and all unit FKs are int.)
    /// </summary>
    public HashSet<int>? VisibleUnitIds { get; init; }

    /// <summary>
    /// Process IDs the user can see. Used when <c>ScopeLevel = "Process"</c>
    /// to filter entities that link to a process (Improvements, Risks, etc.).
    /// </summary>
    public HashSet<string>? VisibleProcessIds { get; init; }

    /// <summary>True when no data filtering is needed (ScopeLevel = All).</summary>
    public bool IsUnscoped => ScopeLevel == "All";

    /// <summary>Singleton for the unscoped (admin) case — avoids allocation.</summary>
    public static readonly ScopeContext Unscoped = new() { ScopeLevel = "All" };
}

/// <summary>
/// Resolves the current user's data-visibility scope from their claims.
/// Registered as Scoped in DI so it's built once per HTTP request.
/// </summary>
public interface IScopingService
{
    /// <summary>
    /// Reads <c>ScopeLevel</c> and <c>OrganizationUnitId</c> claims from the
    /// principal, walks the org tree if needed, and returns a <see cref="ScopeContext"/>
    /// that controllers pass to <c>QueryableScopeExtensions</c>.
    /// </summary>
    Task<ScopeContext> GetScopeAsync(System.Security.Claims.ClaimsPrincipal user);
}
