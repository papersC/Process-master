using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using Microsoft.EntityFrameworkCore;

namespace ESEMS.Web.Services.Bpmn;

/// <summary>
/// Parses <c>&lt;bpmn:lane&gt;</c> elements out of a Process's BPMN diagram,
/// upserts a <see cref="BpmnLane"/> row per lane, attempts to resolve each
/// lane to an existing <see cref="OrganizationUnit"/> (with optional
/// autoCreate fallback), then back-fills <see cref="Activity.OwningUnitId"/>
/// for every activity referenced by the lane's <c>&lt;flowNodeRef&gt;</c>
/// children.
///
/// This is the "best practice" gap closure called out in the audit: lane
/// names in the BPMN XML get a real, queryable link to the org-unit
/// hierarchy instead of staying invisible inside an XML blob.
/// </summary>
public sealed class BpmnLaneReconciler : IBpmnLaneReconciler
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<BpmnLaneReconciler> _logger;

    public BpmnLaneReconciler(ApplicationDbContext db, ILogger<BpmnLaneReconciler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BpmnLaneReconcileResult> ReconcileAsync(
        string processId,
        string bpmnXml,
        bool autoCreateMissingUnits,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processId) || string.IsNullOrWhiteSpace(bpmnXml))
            return BpmnLaneReconcileResult.Empty;

        XDocument doc;
        try { doc = XDocument.Parse(bpmnXml); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BPMN lane reconcile aborted — failed to parse XML for process {ProcessId}", processId);
            return BpmnLaneReconcileResult.Empty;
        }

        // Parse lanes — namespace-agnostic by going through LocalName so we
        // don't care whether the prefix is "bpmn", "bpmn2", or unprefixed.
        //
        // BPMN 2.0 lets lanes nest via <bpmn:childLaneSet>. Descendants()
        // pulls them all out as a flat list, which would double-process the
        // same flowNodeRefs (parent + child both claim ownership). Filter
        // to top-level lanes only — a nested lane is a sub-grouping inside
        // a parent unit, not its own ownership scope. The parent unit owns
        // everything underneath.
        var lanes = doc.Descendants()
            .Where(e => e.Name.LocalName == "lane"
                        && !e.Ancestors().Any(a => a.Name.LocalName == "lane"))
            .ToList();
        if (lanes.Count == 0) return BpmnLaneReconcileResult.Empty;

        // Wrap the whole reconcile in a transaction so a mid-loop failure
        // doesn't leak partial writes (e.g. autoCreate persisted a new
        // OrgUnit but the BpmnLane row insert crashes — without the tx the
        // orphaned unit sticks around forever). Routed through the
        // execution strategy because UseSqlServer is configured with
        // EnableRetryOnFailure, and SqlServerRetryingExecutionStrategy
        // rejects raw BeginTransaction calls. The strategy delegate is the
        // unit of retry — the reconcile loop is read-then-write idempotent
        // (it queries for existing units/lanes before inserting), so a
        // transient retry just re-runs the same logic cleanly.
        BpmnLaneReconcileResult result = BpmnLaneReconcileResult.Empty;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            result = await ReconcileInternalAsync(processId, lanes, autoCreateMissingUnits, actorUserId, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
        return result;
    }

    private async Task<BpmnLaneReconcileResult> ReconcileInternalAsync(
        string processId,
        List<XElement> lanes,
        bool autoCreateMissingUnits,
        string? actorUserId,
        CancellationToken cancellationToken)
    {

        // Load org units once for matching. Filter to active so we don't
        // resurrect retired departments via a fuzzy lane match.
        var units = await _db.OrganizationUnits
            .Where(u => u.IsActive)
            .ToListAsync(cancellationToken);

        var unitByExactNameEn = units
            .GroupBy(u => u.NameEn.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var unitByExactNameAr = units
            .GroupBy(u => u.NameAr.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var unitByNormName = units
            .GroupBy(u => Normalize(u.NameEn), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var unitByNormNameAr = units
            .GroupBy(u => Normalize(u.NameAr), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Synthetic parent for autoCreate fallback. Created on first need so
        // imports that never hit autoCreate don't pollute the hierarchy.
        OrganizationUnit? bpmnImportRoot = null;
        async Task<OrganizationUnit> GetOrCreateImportRootAsync()
        {
            if (bpmnImportRoot != null) return bpmnImportRoot;
            bpmnImportRoot = await _db.OrganizationUnits
                .FirstOrDefaultAsync(u => u.Code == "BPMN_IMPORT", cancellationToken);
            if (bpmnImportRoot == null)
            {
                bpmnImportRoot = new OrganizationUnit
                {
                    Code = "BPMN_IMPORT",
                    NameEn = "BPMN Import (auto-created)",
                    NameAr = "استيراد BPMN (إنشاء تلقائي)",
                    Level = 0,
                    IsActive = true,
                    DisplayOrder = 9999,
                };
                _db.OrganizationUnits.Add(bpmnImportRoot);
                await _db.SaveChangesAsync(cancellationToken);
            }
            return bpmnImportRoot;
        }

        // Pre-load existing lane rows for this process so re-imports update
        // in place instead of creating duplicates.
        var existingByBpmnId = await _db.BpmnLanes
            .Where(l => l.ProcessId == processId)
            .ToDictionaryAsync(l => l.BpmnId, cancellationToken);

        var existingActivities = await _db.Activities
            .Where(a => a.ProcessId == processId)
            .ToListAsync(cancellationToken);

        int matched = 0, autoCreated = 0, unmatched = 0, activitiesBackfilled = 0;
        var seenBpmnIds = new HashSet<string>(StringComparer.Ordinal);
        var nowUtc = DateTime.UtcNow;

        foreach (var laneEl in lanes)
        {
            var bpmnId = laneEl.Attribute("id")?.Value;
            if (string.IsNullOrWhiteSpace(bpmnId)) continue;
            seenBpmnIds.Add(bpmnId);

            // Lane name can come from the @name attribute OR from a child
            // <documentation> element — bpmn-js prefers @name, so do we.
            var name = laneEl.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                // Skip nameless lanes — there's nothing to reconcile against.
                continue;
            }

            // Resolve unit
            int? resolvedUnitId = null;
            string method = BpmnLane.MatchMethods.Pending;

            if (unitByExactNameEn.TryGetValue(name.Trim(), out var hitEn))
            {
                resolvedUnitId = hitEn.Id;
                method = BpmnLane.MatchMethods.Exact;
            }
            else if (unitByExactNameAr.TryGetValue(name.Trim(), out var hitAr))
            {
                resolvedUnitId = hitAr.Id;
                method = BpmnLane.MatchMethods.Exact;
            }
            else
            {
                var norm = Normalize(name);
                if (unitByNormName.TryGetValue(norm, out var hitNormEn))
                {
                    resolvedUnitId = hitNormEn.Id;
                    method = BpmnLane.MatchMethods.Normalized;
                }
                else if (unitByNormNameAr.TryGetValue(norm, out var hitNormAr))
                {
                    resolvedUnitId = hitNormAr.Id;
                    method = BpmnLane.MatchMethods.Normalized;
                }
                else if (autoCreateMissingUnits)
                {
                    var parent = await GetOrCreateImportRootAsync();
                    // 8-char GUID suffix instead of a 3-digit random — eliminates
                    // the birthday-paradox collision risk when many CJK / non-
                    // ASCII lane names all sanitize to "UNIT".
                    var codeSuffix = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
                    var fresh = new OrganizationUnit
                    {
                        Code = $"BPMN_{Sanitize(name)}_{codeSuffix}",
                        NameEn = name,
                        NameAr = name,  // BPMN doesn't carry bilingual lane labels — operator can fix in admin
                        ParentId = parent.Id,
                        Level = 1,
                        IsActive = true,
                    };
                    _db.OrganizationUnits.Add(fresh);
                    await _db.SaveChangesAsync(cancellationToken);
                    units.Add(fresh);
                    unitByExactNameEn[fresh.NameEn] = fresh;
                    resolvedUnitId = fresh.Id;
                    method = BpmnLane.MatchMethods.AutoCreated;
                    autoCreated++;
                }
            }

            if (resolvedUnitId != null && method != BpmnLane.MatchMethods.AutoCreated)
                matched++;
            else if (resolvedUnitId == null)
                unmatched++;

            // Collect flow node refs — these are the activities/tasks/events
            // that live inside this lane. We persist as JSON for audit and
            // use them right now to backfill Activity.OwningUnitId.
            var flowNodeRefs = laneEl.Elements()
                .Where(e => e.Name.LocalName == "flowNodeRef")
                .Select(e => e.Value.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            var flowNodeRefsJson = JsonSerializer.Serialize(flowNodeRefs);

            // Upsert lane row
            if (existingByBpmnId.TryGetValue(bpmnId, out var laneRow))
            {
                laneRow.Name = name;
                // Don't overwrite a previously-Manual resolution with a new
                // auto match — the operator's decision wins on re-import.
                if (laneRow.MatchMethod != BpmnLane.MatchMethods.Manual &&
                    laneRow.MatchMethod != BpmnLane.MatchMethods.Ignored)
                {
                    laneRow.OrganizationUnitId = resolvedUnitId;
                    laneRow.MatchMethod = method;
                    laneRow.MatchedAt = resolvedUnitId != null ? nowUtc : null;
                    laneRow.MatchedById = method == BpmnLane.MatchMethods.AutoCreated ? actorUserId : null;
                }
                laneRow.FlowNodeRefsJson = flowNodeRefsJson;
                laneRow.UpdatedAt = nowUtc;
            }
            else
            {
                laneRow = new BpmnLane
                {
                    ProcessId = processId,
                    BpmnId = bpmnId,
                    Name = name,
                    OrganizationUnitId = resolvedUnitId,
                    MatchMethod = method,
                    MatchedAt = resolvedUnitId != null ? nowUtc : null,
                    MatchedById = method == BpmnLane.MatchMethods.AutoCreated ? actorUserId : null,
                    FlowNodeRefsJson = flowNodeRefsJson,
                };
                _db.BpmnLanes.Add(laneRow);
                existingByBpmnId[bpmnId] = laneRow;
            }

            // Backflow: any Activity row whose Id (or Code) matches one of
            // this lane's flowNodeRefs gets its OwningUnitId set.
            //
            // Important: don't overwrite an OwningUnitId that's already set
            // to something different — that may be a deliberate manual
            // assignment. Only fill nulls and matches.
            if (laneRow.OrganizationUnitId != null)
            {
                foreach (var refId in flowNodeRefs)
                {
                    var activity = existingActivities.FirstOrDefault(a =>
                        a.Id == refId ||
                        string.Equals(a.Code, refId, StringComparison.OrdinalIgnoreCase));
                    if (activity == null) continue;

                    if (activity.OwningUnitId == null ||
                        activity.OwningUnitId == laneRow.OrganizationUnitId)
                    {
                        activity.OwningUnitId = laneRow.OrganizationUnitId;
                        activitiesBackfilled++;
                    }
                }
            }
        }

        // Re-imported BPMN that DROPPED a lane: leave the row in place but
        // mark as orphaned so the operator can clean it up via the UI.
        // (Cheaper than blindly deleting — preserves manual mappings.)
        //
        // CRITICAL: skip rows whose MatchMethod is Manual or Ignored. The
        // operator's decision wins on re-import — silently overwriting a
        // Manual mapping with Ignored would erase deliberate operator work
        // and surprise anyone who saved a mapping yesterday.
        var orphanedCount = 0;
        foreach (var (key, row) in existingByBpmnId)
        {
            if (!seenBpmnIds.Contains(key)
                && row.MatchMethod != BpmnLane.MatchMethods.Ignored
                && row.MatchMethod != BpmnLane.MatchMethods.Manual)
            {
                row.MatchMethod = BpmnLane.MatchMethods.Ignored;
                row.UpdatedAt = nowUtc;
                orphanedCount++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new BpmnLaneReconcileResult(
            LanesSeen: lanes.Count,
            Matched: matched,
            AutoCreated: autoCreated,
            Unmatched: unmatched,
            Orphaned: orphanedCount,
            ActivitiesBackfilled: activitiesBackfilled);
    }

    // Punctuation-strip + collapse whitespace + lowercase. Tolerant of
    // common BPMN modeller habits: " Finance Dept. " == "finance-dept".
    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var chars = s.Trim().ToLower(CultureInfo.InvariantCulture)
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            .ToArray();
        var joined = new string(chars);
        return string.Join(' ', joined.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Limit auto-generated codes to ASCII safe chars
    private static string Sanitize(string s)
    {
        var arr = s.Where(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                   .Take(16)
                   .ToArray();
        return arr.Length == 0 ? "UNIT" : new string(arr).ToUpperInvariant();
    }
}

public interface IBpmnLaneReconciler
{
    Task<BpmnLaneReconcileResult> ReconcileAsync(
        string processId,
        string bpmnXml,
        bool autoCreateMissingUnits,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed record BpmnLaneReconcileResult(
    int LanesSeen,
    int Matched,
    int AutoCreated,
    int Unmatched,
    int Orphaned,
    int ActivitiesBackfilled)
{
    public static readonly BpmnLaneReconcileResult Empty = new(0, 0, 0, 0, 0, 0);
    public bool HasFindings => LanesSeen > 0;
}
