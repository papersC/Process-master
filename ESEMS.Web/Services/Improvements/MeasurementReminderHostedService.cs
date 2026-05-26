using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Services.Notifications;

namespace ESEMS.Web.Services.Improvements;

/// <summary>
/// Long-running background service that wakes up every hour, scans for
/// measurements that are "due" for a reading (per their
/// <see cref="IMeasurementCollectionService.GetDueReadingsAsync"/>
/// definition), and sends a bilingual "reading due" notification to
/// each initiative owner.
///
/// We de-duplicate notifications in-memory by (measurementId, periodLabel)
/// inside one process lifetime — the first time a given period becomes
/// due, we send once. This is best-effort: after a restart the set is
/// cleared, but the notification service itself also writes to the
/// Notifications table so the user sees the same message at most once
/// per session even if we'd otherwise re-send.
///
/// The polling frequency (hourly) is deliberately coarse; measurements
/// use Daily / Weekly / Monthly / Quarterly / Annually cadences so an
/// hour of latency between a period crossing and the user being pinged
/// is acceptable.
/// </summary>
public class MeasurementReminderHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MeasurementReminderHostedService> _logger;
    private readonly HashSet<string> _sentKeys = new();

    public MeasurementReminderHostedService(
        IServiceProvider serviceProvider,
        ILogger<MeasurementReminderHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the app a few seconds after startup to finish migrating &
        // seeding before we start touching the DB.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Measurement reminder sweep failed");
            }

            try { await Task.Delay(PollInterval, stoppingToken); } catch { break; }
        }
    }

    /// <summary>
    /// Single pass: collect all active initiative owners, ask the
    /// collection service which of their measurements are due, fire one
    /// notification per new due row, AND scan ImprovementAction rows whose
    /// due date is within the next 3 days (audit #10) so action assignees
    /// get the same proactive reminder treatment.
    /// </summary>
    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var collection = scope.ServiceProvider.GetRequiredService<IMeasurementCollectionService>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var db = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();

        await ScanActionItemsDueAsync(db, notifier, ct);

        // Find every distinct user id that owns at least one active initiative
        var ownerIds = await Task.Run(() => db.ImprovementInitiatives
            .Where(i => !i.IsDeleted && (i.Status == ImprovementStatus.InProgress || i.Status == ImprovementStatus.OnHold))
            .Select(i => i.OwnerId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList(), ct);

        foreach (var ownerIdStr in ownerIds)
        {
            if (ct.IsCancellationRequested) return;
            if (!int.TryParse(ownerIdStr, out var userId)) continue;

            List<DueReading> due;
            try
            {
                due = await collection.GetDueReadingsAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute due readings for user {UserId}", userId);
                continue;
            }

            foreach (var d in due)
            {
                var key = $"{d.MeasurementId}|{d.PeriodLabel}|{userId}";
                if (_sentKeys.Contains(key)) continue;

                try
                {
                    await notifier.SendAsync(userId,
                        "Measurement reading due",
                        "قراءة مقياس مستحقة",
                        $"Please enter the {d.MeasuringPeriod} reading for '{d.MeasurementNameEn}' ({d.Unit}) on '{d.InitiativeTitleEn}' — period {d.PeriodLabel}.",
                        $"يرجى إدخال القراءة {d.MeasuringPeriod} للمقياس '{d.MeasurementNameAr}' ({d.Unit}) في المبادرة '{d.InitiativeTitleAr}' — الفترة {d.PeriodLabel}.",
                        "Warning",
                        d.InitiativeId,
                        "Improvement",
                        $"/Improvements/Details/{d.InitiativeId}",
                        // BG-006 — pass the stable in-process dedup key down to
                        // the persistent dedup table so a host restart doesn't
                        // re-send the same reading-due notification.
                        dedupKey: key);

                    _sentKeys.Add(key);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send reading-due notification for measurement {MeasurementId}", d.MeasurementId);
                }
            }
        }
    }

    /// <summary>
    /// Audit #10: scans every ImprovementAction whose status is not yet
    /// Completed/Cancelled and whose DueDate is within the next 3 days (or
    /// already overdue). For each action we ping both the assignee and the
    /// initiative owner once per (actionId, dueDate) tuple. Re-runs are
    /// silently de-duplicated against the same in-memory _sentKeys set as
    /// measurement reminders so a restart re-pings, but within a single
    /// process lifetime we never spam.
    /// </summary>
    private async Task ScanActionItemsDueAsync(
        Data.ApplicationDbContext db,
        INotificationService notifier,
        CancellationToken ct)
    {
        var horizon = DateTime.UtcNow.AddDays(3);

        var dueActions = await db.ImprovementActions
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                        && a.Status != ImprovementActionStatus.Completed
                        && a.Status != ImprovementActionStatus.Cancelled
                        && a.DueDate.HasValue
                        && a.DueDate.Value <= horizon)
            .Include(a => a.Improvement)
            .ToListAsync(ct);

        foreach (var a in dueActions)
        {
            if (ct.IsCancellationRequested) return;
            if (a.Improvement == null || a.Improvement.IsDeleted) continue;

            var due = a.DueDate!.Value.ToString("yyyy-MM-dd");
            var overdue = a.DueDate.Value < DateTime.UtcNow;
            var titleEn = overdue ? "Action item overdue" : "Action item due soon";
            var titleAr = overdue ? "بند الإجراء متأخر" : "بند الإجراء يقترب موعده";
            var sevWhen = overdue ? "Warning" : "Info";

            // Notify assignee (if any).
            if (!string.IsNullOrWhiteSpace(a.AssignedToId)
                && int.TryParse(a.AssignedToId, out var assigneeId)
                && assigneeId != 0)
            {
                var key = $"action|{a.Id}|{due}|{assigneeId}";
                if (!_sentKeys.Contains(key))
                {
                    try
                    {
                        await notifier.SendAsync(assigneeId,
                            titleEn, titleAr,
                            $"Action '{a.NameEn}' on initiative '{a.Improvement.TitleEn}' is due {due}.",
                            $"بند الإجراء '{a.NameAr}' في المبادرة '{a.Improvement.TitleAr}' مستحق في {due}.",
                            sevWhen,
                            a.Improvement.Id,
                            "Improvement",
                            $"/Improvements/Details/{a.Improvement.Id}#actions",
                            // BG-006 — persistent dedup across host restarts.
                            dedupKey: key);
                        _sentKeys.Add(key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send action-due notification for action {ActionId}", a.Id);
                    }
                }
            }

            // Notify initiative owner if not the assignee.
            if (!string.IsNullOrWhiteSpace(a.Improvement.OwnerId)
                && int.TryParse(a.Improvement.OwnerId, out var ownerId)
                && ownerId != 0
                && a.Improvement.OwnerId != a.AssignedToId)
            {
                var key = $"action|{a.Id}|{due}|owner-{ownerId}";
                if (!_sentKeys.Contains(key))
                {
                    try
                    {
                        await notifier.SendAsync(ownerId,
                            titleEn, titleAr,
                            $"Action '{a.NameEn}' on '{a.Improvement.TitleEn}' is due {due}.",
                            $"بند الإجراء '{a.NameAr}' في '{a.Improvement.TitleAr}' مستحق في {due}.",
                            sevWhen,
                            a.Improvement.Id,
                            "Improvement",
                            $"/Improvements/Details/{a.Improvement.Id}#actions",
                            // BG-006 — persistent dedup across host restarts.
                            dedupKey: key);
                        _sentKeys.Add(key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send action-due owner notification for action {ActionId}", a.Id);
                    }
                }
            }
        }
    }
}
