using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Services.Notifications;

namespace ESEMS.Web.Services.Improvements;

/// <summary>
/// Audit #1 — DGEP 4G Strategic Management §2.4 (Benefits Realization).
///
/// Polls every 6 hours and does two things:
///
/// 1. <b>Schedule</b>: For every Closed initiative that does NOT yet have
///    benefits-review rows, insert three checkpoint rows (3M / 6M / 12M
///    after the closure date). Idempotent — re-running is a no-op once
///    the rows exist.
///
/// 2. <b>Notify</b>: For every Pending review whose <see cref="ImprovementBenefitsReview.DueDate"/>
///    is within the next 7 days (or already past), send a one-shot
///    bilingual reminder to the initiative owner. De-duplicated in-process
///    by (reviewId, ownerId).
///
/// Lifecycle transitions (Closed → BenefitsRealization, BenefitsRealization
/// → Sustained) are NOT done by this service — they're user-driven via the
/// state machine. This service just keeps the underlying review rows fresh.
/// </summary>
public class BenefitsRealizationScheduler : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan ReminderHorizon = TimeSpan.FromDays(7);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BenefitsRealizationScheduler> _logger;
    private readonly HashSet<string> _sentReminderKeys = new();

    public BenefitsRealizationScheduler(
        IServiceProvider serviceProvider,
        ILogger<BenefitsRealizationScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Same startup grace period as the measurement reminder service.
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Benefits realization scheduler sweep failed");
            }

            try { await Task.Delay(PollInterval, stoppingToken); } catch { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();

        await ScheduleNewCheckpointsAsync(db, ct);
        await NotifyDueReviewsAsync(db, notifier, ct);
    }

    /// <summary>
    /// For every Closed (or BenefitsRealization-or-later) initiative that has
    /// no benefits-review rows yet, create the 3M/6M/12M checkpoints based on
    /// the closure-report ClosedAt timestamp.
    /// </summary>
    private static async Task ScheduleNewCheckpointsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var closedNeedingSchedule = await db.ImprovementInitiatives
            .AsNoTracking()
            .Where(i => !i.IsDeleted
                        && (i.Status == ImprovementStatus.Closed
                            || i.Status == ImprovementStatus.BenefitsRealization
                            || i.Status == ImprovementStatus.Sustained))
            .Join(db.ImprovementClosureReports.AsNoTracking(),
                  i => i.Id,
                  c => c.ImprovementId,
                  (i, c) => new { Initiative = i, c.ClosedAt })
            .Where(x => !db.Set<ImprovementBenefitsReview>().Any(b => b.ImprovementId == x.Initiative.Id))
            .ToListAsync(ct);

        if (closedNeedingSchedule.Count == 0) return;

        // Suspend the change-log interceptor — these are system-driven inserts,
        // not user edits worth recording in the per-initiative history.
        using var _ = SuspendImprovementChangeLog.Begin();

        foreach (var x in closedNeedingSchedule)
        {
            db.Set<ImprovementBenefitsReview>().AddRange(
                new ImprovementBenefitsReview
                {
                    ImprovementId = x.Initiative.Id,
                    Period = BenefitsReviewPeriod.ThreeMonths,
                    DueDate = x.ClosedAt.AddMonths(3),
                },
                new ImprovementBenefitsReview
                {
                    ImprovementId = x.Initiative.Id,
                    Period = BenefitsReviewPeriod.SixMonths,
                    DueDate = x.ClosedAt.AddMonths(6),
                },
                new ImprovementBenefitsReview
                {
                    ImprovementId = x.Initiative.Id,
                    Period = BenefitsReviewPeriod.TwelveMonths,
                    DueDate = x.ClosedAt.AddMonths(12),
                });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Ping owners about Pending reviews within the 7-day window.
    /// </summary>
    private async Task NotifyDueReviewsAsync(ApplicationDbContext db, INotificationService notifier, CancellationToken ct)
    {
        var horizon = DateTime.UtcNow.Add(ReminderHorizon);

        var dueReviews = await db.Set<ImprovementBenefitsReview>()
            .AsNoTracking()
            .Where(b => b.Outcome == BenefitsReviewOutcome.Pending && b.DueDate <= horizon)
            .Include(b => b.Improvement)
            .ToListAsync(ct);

        foreach (var b in dueReviews)
        {
            if (ct.IsCancellationRequested) return;
            if (b.Improvement == null || b.Improvement.IsDeleted) continue;
            if (string.IsNullOrWhiteSpace(b.Improvement.OwnerId)) continue;
            if (!int.TryParse(b.Improvement.OwnerId, out var ownerId) || ownerId == 0) continue;

            var key = $"benefits|{b.Id}|{ownerId}";
            if (_sentReminderKeys.Contains(key)) continue;

            var months = (int)b.Period;
            var overdue = b.DueDate < DateTime.UtcNow;
            var titleEn = overdue ? "Benefits review overdue" : "Benefits review due soon";
            var titleAr = overdue ? "مراجعة الأثر متأخرة" : "مراجعة الأثر تقترب موعدها";

            try
            {
                await notifier.SendAsync(ownerId,
                    titleEn, titleAr,
                    $"The {months}-month benefits review for '{b.Improvement.TitleEn}' is due {b.DueDate:yyyy-MM-dd}.",
                    $"مراجعة الأثر بعد {months} أشهر للمبادرة '{b.Improvement.TitleAr}' مستحقة في {b.DueDate:yyyy-MM-dd}.",
                    overdue ? "Warning" : "Info",
                    b.Improvement.Id,
                    "Improvement",
                    $"/Improvements/Details/{b.Improvement.Id}#benefits",
                    // BG-007 — persistent dedup across host restarts.
                    dedupKey: key);
                _sentReminderKeys.Add(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send benefits-review reminder for review {ReviewId}", b.Id);
            }
        }
    }
}
