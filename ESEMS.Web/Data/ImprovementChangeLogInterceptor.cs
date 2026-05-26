using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ESEMS.Web.Models.Improvement;

namespace ESEMS.Web.Data;

/// <summary>
/// EF SaveChanges interceptor that records every property change on an
/// <see cref="ImprovementInitiative"/> into <see cref="ImprovementChangeLog"/>
/// (audit #9). Runs synchronously inside the same transaction as the parent
/// save, so either both the entity update and the change-log row commit, or
/// neither does.
///
/// Skip rules:
///   - Audit / housekeeping fields (UpdatedAt / UpdatedById / Version /
///     CreatedAt / CreatedById) are NOT logged — they're tautological and
///     just generate noise.
///   - The "Code" field is also skipped (auto-generated server-side, never
///     a meaningful user change).
///   - Calculated/computed properties (BusinessReadinessScore /
///     BusinessValueScore / TotalPrioritizationScore / Quadrant) are
///     downstream of the fields the user actually changed — logging them
///     too would double-count the same edit.
///
/// To opt out of logging for a particular SaveChanges call (e.g. a
/// system-driven background update), set
/// <c>db.ChangeTracker.LazyLoadingEnabled = false</c> won't help — instead
/// use the <see cref="SuspendImprovementChangeLog"/> AsyncLocal flag below.
/// </summary>
public sealed class ImprovementChangeLogInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> SkipFields = new(StringComparer.Ordinal)
    {
        nameof(ImprovementInitiative.UpdatedAt),
        nameof(ImprovementInitiative.UpdatedById),
        nameof(ImprovementInitiative.Version),
        nameof(ImprovementInitiative.CreatedAt),
        nameof(ImprovementInitiative.CreatedById),
        nameof(ImprovementInitiative.Code),
        nameof(ImprovementInitiative.Quadrant), // derived from Impact/Effort
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ImprovementChangeLogInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (SuspendImprovementChangeLog.IsSuspended)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        if (eventData.Context is not ApplicationDbContext db)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var actor = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        var stamp = DateTime.UtcNow;

        // Snapshot the modified entries first — adding ChangeLog rows below
        // would mutate the change tracker mid-iteration.
        var changes = db.ChangeTracker.Entries<ImprovementInitiative>()
            .Where(e => e.State == EntityState.Modified)
            .SelectMany(e => CollectChanges(e, actor, stamp))
            .ToList();

        if (changes.Count > 0)
            db.Set<ImprovementChangeLog>().AddRange(changes);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        // Sync path mirrors the async one; identical logic.
        if (SuspendImprovementChangeLog.IsSuspended)
            return base.SavingChanges(eventData, result);

        if (eventData.Context is not ApplicationDbContext db)
            return base.SavingChanges(eventData, result);

        var actor = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        var stamp = DateTime.UtcNow;

        var changes = db.ChangeTracker.Entries<ImprovementInitiative>()
            .Where(e => e.State == EntityState.Modified)
            .SelectMany(e => CollectChanges(e, actor, stamp))
            .ToList();

        if (changes.Count > 0)
            db.Set<ImprovementChangeLog>().AddRange(changes);

        return base.SavingChanges(eventData, result);
    }

    private static IEnumerable<ImprovementChangeLog> CollectChanges(
        EntityEntry<ImprovementInitiative> entry,
        string? actor,
        DateTime stamp)
    {
        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified) continue;
            if (SkipFields.Contains(prop.Metadata.Name)) continue;

            var oldVal = prop.OriginalValue?.ToString();
            var newVal = prop.CurrentValue?.ToString();
            if (oldVal == newVal) continue;

            yield return new ImprovementChangeLog
            {
                ImprovementId = entry.Entity.Id,
                FieldName = prop.Metadata.Name,
                OldValue = Truncate(oldVal, 2000),
                NewValue = Truncate(newVal, 2000),
                ChangedById = actor,
                ChangedAt = stamp
            };
        }
    }

    private static string? Truncate(string? s, int max)
        => s is null ? null : (s.Length <= max ? s : s.Substring(0, max));
}

/// <summary>
/// AsyncLocal scope-flag for code paths that need to bypass the change log
/// (e.g. seeders, migrations-as-code, bulk imports). Use with <c>using</c>
/// to ensure it's always reset.
/// </summary>
public static class SuspendImprovementChangeLog
{
    private static readonly AsyncLocal<bool> _suspended = new();
    public static bool IsSuspended => _suspended.Value;

    public static IDisposable Begin()
    {
        _suspended.Value = true;
        return new Restore();
    }

    private sealed class Restore : IDisposable
    {
        public void Dispose() => _suspended.Value = false;
    }
}
