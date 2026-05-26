using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Services.Notifications;

namespace ESEMS.Web.Services.Improvements;

/// <summary>
/// Audit #5 — DGEP 4G Process Management §4.3 (proactive intervention).
///
/// Polls on a cadence (default 12h) and ranks every InProgress / OnHold
/// initiative against three escalation thresholds based on inactivity:
///
///   - <b>Amber</b> with no measurement reading and no status change.
///     Pings the initiative owner.
///   - <b>Red</b>. Pings the owner AND the OwningUnit head (escalation).
///   - <b>Critical</b>. Pings owner, unit head, AND any user with the
///     ADMIN role.
///
/// "Inactivity" = max(UpdatedAt, latest MeasurementReading.EnteredAt,
/// latest ImprovementReview.CreatedAt). The service de-duplicates by
/// (initiativeId, severityBand) per process lifetime so an initiative
/// stuck at Red doesn't spam the owner every poll.
///
/// Cadence + thresholds are bound from the <c>StallDetection</c> section in
/// appsettings.json via <see cref="StallDetectionOptions"/> — defaults
/// remain 12h / 14 / 30 / 60.
/// </summary>
public class InitiativeStallDetectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InitiativeStallDetectionService> _logger;
    private readonly StallDetectionOptions _options;
    private readonly TimeSpan _pollInterval;
    private readonly HashSet<string> _sentKeys = new();

    public InitiativeStallDetectionService(
        IServiceProvider serviceProvider,
        ILogger<InitiativeStallDetectionService> logger,
        IOptions<StallDetectionOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _pollInterval = TimeSpan.FromHours(Math.Max(1, _options.PollIntervalHours));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stall detection sweep failed");
            }

            try { await Task.Delay(_pollInterval, stoppingToken); } catch { break; }
        }
    }

    // Per-(recipient, band) bucket built up during a sweep so DispatchAsync
    // can fold N same-second notifications into one digest. Reset each sweep.
    private sealed record InitiativeRef(string Id, string TitleEn, string TitleAr, int IdleDays);
    private sealed record BucketKey(int RecipientId, string Band, string Role);

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var pending = new Dictionary<BucketKey, List<InitiativeRef>>();

        var candidates = await db.ImprovementInitiatives
            .AsNoTracking()
            .Where(i => !i.IsDeleted
                        && (i.Status == ImprovementStatus.InProgress
                            || i.Status == ImprovementStatus.OnHold))
            .Select(i => new
            {
                i.Id,
                i.OwnerId,
                i.OwningUnitId,
                i.TitleEn,
                i.TitleAr,
                i.UpdatedAt,
                LatestReading = db.Set<Models.Improvement.MeasurementReading>()
                    .Where(r => r.EnteredAt.HasValue
                                && db.ImprovementMeasurements.Any(m =>
                                    m.Id == r.MeasurementId && m.ImprovementId == i.Id))
                    .Max(r => (DateTime?)r.EnteredAt),
                LatestReview = db.Set<Models.Improvement.ImprovementReview>()
                    .Where(r => r.ImprovementId == i.Id)
                    .Max(r => (DateTime?)r.CreatedAt)
            })
            .ToListAsync(ct);

        // Pre-resolve admin recipients once per sweep so we don't query per
        // Critical initiative.
        List<int>? adminIdsCached = null;

        foreach (var c in candidates)
        {
            if (ct.IsCancellationRequested) return;

            var lastActivity = new[] { c.UpdatedAt, c.LatestReading ?? DateTime.MinValue, c.LatestReview ?? DateTime.MinValue }.Max();
            var idleDays = (int)(now - lastActivity).TotalDays;

            string? band = idleDays >= _options.CriticalDays ? "Critical"
                         : idleDays >= _options.RedDays      ? "Red"
                         : idleDays >= _options.AmberDays    ? "Amber"
                         : null;
            if (band is null) continue;

            var key = $"{c.Id}|{band}";
            if (_sentKeys.Contains(key)) continue;
            _sentKeys.Add(key);

            await EnqueueRecipientsAsync(db, pending, c.Id, c.OwnerId, c.OwningUnitId,
                                         c.TitleEn, c.TitleAr, idleDays, band,
                                         async () => adminIdsCached ??= await db.UserRoleGroups
                                             .AsNoTracking()
                                             .Where(urg => urg.RoleGroup != null && urg.RoleGroup.Code == "administrator")
                                             .Select(urg => urg.UserId)
                                             .Distinct()
                                             .ToListAsync(ct),
                                         ct);
        }

        await DispatchAsync(notifier, pending, ct);
    }

    private async Task EnqueueRecipientsAsync(
        ApplicationDbContext db,
        Dictionary<BucketKey, List<InitiativeRef>> pending,
        string initiativeId,
        string? ownerId,
        int? owningUnitId,
        string titleEn,
        string titleAr,
        int idleDays,
        string band,
        Func<Task<List<int>>> getAdminIds,
        CancellationToken ct)
    {
        var item = new InitiativeRef(initiativeId, titleEn, titleAr, idleDays);

        void Add(int recipientId, string role)
        {
            if (recipientId == 0) return;
            var key = new BucketKey(recipientId, band, role);
            if (!pending.TryGetValue(key, out var list))
            {
                list = new List<InitiativeRef>();
                pending[key] = list;
            }
            list.Add(item);
        }

        // Always notify the owner.
        if (!string.IsNullOrWhiteSpace(ownerId) && int.TryParse(ownerId, out var ownerIdInt))
            Add(ownerIdInt, "Owner");

        // Red and above: escalate to OwningUnit head.
        if (band != "Amber" && owningUnitId != null)
        {
            var unitHead = await db.OrganizationUnits
                .AsNoTracking()
                .Where(o => o.Id == owningUnitId)
                .Select(o => o.HeadUserId)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(unitHead)
                && int.TryParse(unitHead, out var unitHeadId) && unitHead != ownerId)
                Add(unitHeadId, "UnitHead");
        }

        // Critical: also notify ADMIN role group.
        if (band == "Critical")
        {
            var adminIds = await getAdminIds();
            foreach (var adminId in adminIds)
            {
                if (adminId.ToString() == ownerId) continue;
                Add(adminId, "Admin");
            }
        }
    }

    private async Task DispatchAsync(
        INotificationService notifier,
        Dictionary<BucketKey, List<InitiativeRef>> pending,
        CancellationToken ct)
    {
        foreach (var (key, items) in pending)
        {
            if (ct.IsCancellationRequested) return;
            var (titleEn, titleAr, msgEn, msgAr, entityId, url) = BuildMessage(key, items);
            var sev = key.Band == "Amber" ? "Info" : "Warning";

            // Stable dedup key: doesn't bake in the minIdle day count, so
            // a 90→91 tick during the day no longer slips past the dedup
            // guard. Granular enough that distinct (band, role) pairs still
            // surface as separate notifications.
            var dedupKey = $"stall:{key.RecipientId}:{key.Band}:{key.Role}";
            try
            {
                await notifier.SendAsync(key.RecipientId, titleEn, titleAr, msgEn, msgAr, sev,
                    entityId, "Improvement", url, dedupKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver stall digest to {RecipientId} ({Role}, {Band}, {Count} items)",
                    key.RecipientId, key.Role, key.Band, items.Count);
            }
        }
    }

    /// <summary>
    /// Builds a user-facing notification (title + body, EN/AR) for one
    /// recipient bucket. Drops band jargon (Amber/Red/Critical) in favour of
    /// concrete idle-day counts and leads with an action verb so the bell
    /// preview reads as a task rather than a status.
    /// </summary>
    private (string TitleEn, string TitleAr, string MsgEn, string MsgAr, string EntityId, string Url) BuildMessage(
        BucketKey key, List<InitiativeRef> items)
    {
        // Role chip — kept minimal so non-owner recipients can tell why they
        // received the notification, without the "admin alert" robot-speak.
        var roleSuffixEn = key.Role switch
        {
            "UnitHead" => " — your unit",
            "Admin"    => " — admin notice",
            _          => ""
        };
        var roleSuffixAr = key.Role switch
        {
            "UnitHead" => " — وحدتك",
            "Admin"    => " — إشعار إداري",
            _          => ""
        };

        if (items.Count == 1)
        {
            var item = items[0];
            var titleEn = $"Action needed — initiative idle {item.IdleDays} days{roleSuffixEn}";
            var titleAr = $"إجراء مطلوب — مبادرة بدون نشاط منذ {item.IdleDays} يوماً{roleSuffixAr}";
            var msgEn = $"{item.TitleEn} has had no activity for {item.IdleDays} days. Open the initiative to update progress or change its status.";
            var msgAr = $"{item.TitleAr} لم تشهد أي نشاط منذ {item.IdleDays} يوماً. افتح المبادرة لتحديث التقدم أو تغيير الحالة.";
            return (titleEn, titleAr, msgEn, msgAr, item.Id, $"/Improvements/Details/{item.Id}");
        }

        // Digest path — N initiatives in the same band, one notification.
        // Use the LOWEST idle-day count in the batch so the wording is
        // factually accurate ("all idle at least X days") rather than
        // anchored on the band threshold.
        var n = items.Count;
        var minIdle = items.Min(i => i.IdleDays);

        // Names: no surrounding quotes (looks like code output), separator
        // matches the locale, "+N more" parenthetical instead of trailing
        // " and N more" which reads like prose.
        var samples = items.OrderByDescending(i => i.IdleDays).Take(3).ToList();
        var sampleEn = string.Join(", ", samples.Select(i => i.TitleEn));
        var sampleAr = string.Join("، ", samples.Select(i => i.TitleAr));
        var extra = n - samples.Count;
        var moreEn = extra > 0 ? $" (+{extra} more)" : "";
        var moreAr = extra > 0 ? $" (+{extra} أخرى)" : "";

        var titleEnDigest = $"Action needed — {n} initiatives idle {minIdle}+ days{roleSuffixEn}";
        var titleArDigest = $"إجراء مطلوب — {n} مبادرات بدون نشاط منذ {minIdle}+ يوماً{roleSuffixAr}";
        var msgEnDigest = $"Examples: {sampleEn}{moreEn}. Triage the full list at My Approvals → Stalled.";
        var msgArDigest = $"أمثلة: {sampleAr}{moreAr}. راجع القائمة الكاملة في موافقاتي ← المتوقفة.";
        return (titleEnDigest, titleArDigest, msgEnDigest, msgArDigest, items[0].Id, "/Improvements?statusFilter=Stalled");
    }
}
