using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Services.Auditing;

/// <summary>
/// EF Core SaveChanges interceptor that writes AuditLog rows for Create/Update/Delete.
///
/// EntityId resolution is two-phase:
///  1. SavingChanges builds the AuditLog rows. For Added entities with auto-
///     generated identity keys (e.g. CustomUser.UserId), the primary key in the
///     change tracker at this point is a negative sentinel (~int.MinValue).
///     Writing those rows here would persist EntityId = "-2147482647".
///  2. SavedChanges runs AFTER the database has populated the real keys —
///     deferred AuditLog rows are filled in with the real EntityId and saved
///     in a second SaveChanges call (the audit-log filter at the top of
///     TryCreateAuditLogs skips those rows so they don't recurse).
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Per-DbContext list of audit rows whose EntityId could only be resolved
    // after SaveChanges. ConcurrentDictionary because the interceptor is
    // registered as a singleton-style service and may run across requests.
    private readonly ConcurrentDictionary<DbContext, List<(AuditLog Log, EntityEntry Entry)>> _deferred = new();

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuditSaveChangesInterceptor> _logger;

    public AuditSaveChangesInterceptor(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<AuditSaveChangesInterceptor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        TryCreateAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        TryCreateAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushDeferredAuditLogs(eventData.Context, isAsync: false).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await FlushDeferredAuditLogs(eventData.Context, isAsync: true, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task FlushDeferredAuditLogs(DbContext? context, bool isAsync, CancellationToken cancellationToken = default)
    {
        if (context == null) return;
        if (!_deferred.TryRemove(context, out var pending) || pending.Count == 0) return;

        try
        {
            foreach (var (log, entry) in pending)
                log.EntityId = TryGetPrimaryKey(entry);

            context.Set<AuditLog>().AddRange(pending.Select(p => p.Log));

            // The inner SaveChanges re-enters the interceptor pipeline. The
            // existing `e.Entity is not AuditLog` filter in TryCreateAuditLogs
            // skips these rows, and FlushDeferredAuditLogs is a no-op when the
            // deferred list is empty — so the recursion bottoms out in one step.
            if (isAsync)
                await context.SaveChangesAsync(cancellationToken);
            else
                context.SaveChanges();
        }
        catch (Exception ex)
        {
            // Audit-log persistence must never throw out of the business save.
            _logger.LogError(ex, "Failed to flush deferred audit logs.");
        }
    }

    private void TryCreateAuditLogs(DbContext? context)
    {
        try
        {
            if (context == null) return;

            context.ChangeTracker.DetectChanges();

            var auditIdentityChanges = _configuration.GetValue("Auditing:AuditIdentityChanges", false);

            var http = _httpContextAccessor.HttpContext;
            var principal = http?.User;
            var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = principal?.Identity?.Name;

            var ip = http?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = http?.Request?.Headers.UserAgent.ToString();
            var now = DateTime.UtcNow;

            var entries = context.ChangeTracker
                .Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => e.Entity is not AuditLog)
                .ToList();

            if (entries.Count == 0) return;

            var logs = new List<AuditLog>();
            foreach (var entry in entries)
            {
                var clrType = entry.Metadata.ClrType;
                var ns = clrType.Namespace ?? string.Empty;

                // Only audit application models by default.
                if (!ns.StartsWith("ESEMS.Web.Models", StringComparison.Ordinal))
                    continue;

                // Identity/user rows can be very noisy (e.g., LastLoginAt updates).
                if (!auditIdentityChanges && string.Equals(clrType.Name, "User", StringComparison.Ordinal))
                    continue;

                var action = entry.State switch
                {
                    EntityState.Added => AuditAction.Create,
                    EntityState.Modified => AuditAction.Update,
                    EntityState.Deleted => AuditAction.Delete,
                    _ => AuditAction.Update
                };

                var entityType = clrType.Name;
                var entityName = TryGetEntityName(entry);
                var (oldValues, newValues, changedProperties) = ExtractValues(entry);

                if (action == AuditAction.Update && changedProperties.Count == 0)
                    continue;

                // For Added entries with auto-generated identity keys, the
                // primary key in the change tracker right now is a temporary
                // negative sentinel (around int.MinValue) — EF only fills the
                // real value AFTER SaveChanges. Defer those rows so SavedChanges
                // can stamp the real EntityId. Modified/Deleted rows already
                // have their real keys, so they're written inline as before.
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    UserName = userName,
                    Action = action,
                    EntityType = entityType,
                    EntityId = string.Empty, // overwritten below for non-deferred rows
                    EntityName = entityName,
                    OldValues = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
                    NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues, JsonOptions),
                    ChangedProperties = changedProperties.Count == 0 ? null : string.Join(", ", changedProperties),
                    IpAddress = ip,
                    UserAgent = userAgent,
                    Timestamp = now
                };

                if (entry.State == EntityState.Added && HasAutoGeneratedKey(entry))
                {
                    // EntityId stays empty for now; SavedChanges fills it in.
                    var deferredList = _deferred.GetOrAdd(context, _ => new List<(AuditLog, EntityEntry)>());
                    deferredList.Add((auditLog, entry));
                }
                else
                {
                    auditLog.EntityId = TryGetPrimaryKey(entry);
                    logs.Add(auditLog);
                }
            }

            if (logs.Count > 0)
            {
                context.Set<AuditLog>().AddRange(logs);
            }
        }
        catch (Exception ex)
        {
            // Never break business transactions because of auditing.
            _logger.LogError(ex, "Failed to write audit logs.");
        }
    }

    /// <summary>
    /// True when the entity's primary key is database-generated (e.g. SQL
    /// Server IDENTITY). For these, the change tracker holds a temp negative
    /// sentinel during SavingChanges and the real key only appears after the
    /// INSERT. Entities with application-supplied keys (string Guid defaults
    /// on most ESEMS models) already have their real key in the tracker.
    /// </summary>
    private static bool HasAutoGeneratedKey(EntityEntry entry)
    {
        var keyProps = entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).ToList();
        if (keyProps.Count == 0) return false;
        foreach (var p in keyProps)
        {
            if (p.Metadata.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd
                || p.Metadata.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate)
                return true;
        }
        return false;
    }

    private static string TryGetPrimaryKey(EntityEntry entry)
    {
        var keyProps = entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).ToList();
        if (keyProps.Count == 0) return string.Empty;

        if (keyProps.Count == 1)
        {
            return keyProps[0].CurrentValue?.ToString()
                   ?? keyProps[0].OriginalValue?.ToString()
                   ?? string.Empty;
        }

        return string.Join("|", keyProps.Select(p => p.CurrentValue?.ToString() ?? p.OriginalValue?.ToString() ?? string.Empty));
    }

    private static string? TryGetEntityName(EntityEntry entry)
    {
        // Common naming patterns in this project.
        foreach (var nameProp in new[] { "NameEn", "Name", "Title", "Code" })
        {
            try
            {
                var p = entry.Property(nameProp);
                var v = p?.CurrentValue?.ToString() ?? p?.OriginalValue?.ToString();
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            catch (InvalidOperationException)
            {
                // Property doesn't exist on this entity type — EF's Property()
                // throws InvalidOperationException in that case. We intentionally
                // swallow this specific exception only; bare `catch {}` was
                // swallowing OutOfMemoryException and ThreadAbortException too.
            }
        }
        return null;
    }

    private static (Dictionary<string, object?> OldValues, Dictionary<string, object?> NewValues, List<string> Changed)
        ExtractValues(EntityEntry entry)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        var changed = new List<string>();

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey()) continue;
            if (property.Metadata.IsShadowProperty()) continue;

            var name = property.Metadata.Name;

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[name] = property.CurrentValue;
                    break;
                case EntityState.Deleted:
                    oldValues[name] = property.OriginalValue;
                    break;
                case EntityState.Modified:
                    if (!property.IsModified) continue;
                    oldValues[name] = property.OriginalValue;
                    newValues[name] = property.CurrentValue;
                    changed.Add(name);
                    break;
            }
        }

        return (oldValues, newValues, changed);
    }
}
