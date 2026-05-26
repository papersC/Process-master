using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Services.Notifications;

namespace ESEMS.Web.Services.Improvements;

/// <summary>
/// Default <see cref="IMeasurementCollectionService"/> implementation. Keeps
/// the cadence math in one place so the reminder background service, the
/// Details-page banner, and the Record-Reading POST handler all agree on
/// which period "now" falls into.
/// </summary>
public class MeasurementCollectionService : IMeasurementCollectionService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MeasurementCollectionService> _logger;

    public MeasurementCollectionService(
        ApplicationDbContext context,
        INotificationService notificationService,
        ILogger<MeasurementCollectionService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // Cadence / period math
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the canonical (label, start) tuple for the period that
    /// contains <paramref name="at"/>. Supports the cadence strings the
    /// wizard emits: Daily / Weekly / Monthly / Quarterly / Annually.
    /// Unknown / null cadences are treated as Monthly.
    /// </summary>
    public (string PeriodLabel, DateTime PeriodStart) PeriodFor(ImprovementMeasurement measurement, DateTime at)
    {
        var cadence = (measurement.MeasuringPeriod ?? "Monthly").Trim();
        at = at.ToUniversalTime();

        switch (cadence)
        {
            case "Daily":
                {
                    var start = new DateTime(at.Year, at.Month, at.Day, 0, 0, 0, DateTimeKind.Utc);
                    return (start.ToString("yyyy-MM-dd"), start);
                }
            case "Weekly":
                {
                    var culture = CultureInfo.InvariantCulture;
                    var cal = culture.Calendar;
                    // ISO-8601 week: week 1 is the week with Thursday in it.
                    // .NET's GetWeekOfYear with FirstFourDayWeek + Monday matches ISO.
                    var week = cal.GetWeekOfYear(at, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    // Align start to the Monday of that week
                    var diff = ((int)at.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                    var start = new DateTime(at.Year, at.Month, at.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-diff);
                    return ($"{at.Year:D4}-W{week:D2}", start);
                }
            case "Quarterly":
                {
                    var quarter = ((at.Month - 1) / 3) + 1;
                    var start = new DateTime(at.Year, ((quarter - 1) * 3) + 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    return ($"{at.Year:D4}-Q{quarter}", start);
                }
            case "Annually":
                {
                    var start = new DateTime(at.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    return (at.Year.ToString("D4"), start);
                }
            case "Monthly":
            default:
                {
                    var start = new DateTime(at.Year, at.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    return ($"{at.Year:D4}-{at.Month:D2}", start);
                }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Due readings
    // ─────────────────────────────────────────────────────────────────

    public async Task<List<DueReading>> GetDueReadingsAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var userOwnedFilter = userId.ToString();

        // Pull all measurements for initiatives that the user owns and
        // whose initiative is currently in an executing state. We filter
        // the cadence + last-reading logic in memory afterward since
        // PeriodFor() is not translatable to SQL.
        var candidates = await _context.ImprovementMeasurements
            .AsNoTracking()
            .Include(m => m.Improvement)
            .Where(m => m.Improvement != null
                        && !m.Improvement.IsDeleted
                        && (m.Improvement.Status == ImprovementStatus.InProgress || m.Improvement.Status == ImprovementStatus.OnHold)
                        && m.Improvement.OwnerId == userOwnedFilter)
            .ToListAsync();

        if (candidates.Count == 0) return new List<DueReading>();

        var ids = candidates.Select(c => c.Id).ToList();
        var latestReadings = await _context.MeasurementReadings
            .AsNoTracking()
            .Where(r => ids.Contains(r.MeasurementId))
            .GroupBy(r => r.MeasurementId)
            .Select(g => new { MeasurementId = g.Key, MaxStart = g.Max(x => x.PeriodStart) })
            .ToDictionaryAsync(x => x.MeasurementId, x => x.MaxStart);

        var due = new List<DueReading>();
        foreach (var m in candidates)
        {
            var currentPeriod = PeriodFor(m, now);
            // "Due" means either no readings yet, or the latest reading is
            // strictly before the current period's start.
            var lastStart = latestReadings.TryGetValue(m.Id, out var ls) ? ls : (DateTime?)null;
            if (lastStart.HasValue && lastStart.Value >= currentPeriod.PeriodStart)
                continue;

            due.Add(new DueReading(
                MeasurementId: m.Id,
                InitiativeId: m.ImprovementId,
                InitiativeTitleEn: m.Improvement?.TitleEn ?? string.Empty,
                InitiativeTitleAr: m.Improvement?.TitleAr ?? string.Empty,
                MeasurementNameEn: m.NameEn ?? string.Empty,
                MeasurementNameAr: m.NameAr ?? string.Empty,
                Unit: m.UnitOfMeasure,
                PeriodLabel: currentPeriod.PeriodLabel,
                PeriodStart: currentPeriod.PeriodStart,
                MeasuringPeriod: m.MeasuringPeriod ?? "Monthly"
            ));
        }

        return due;
    }

    // ─────────────────────────────────────────────────────────────────
    // Record a new reading
    // ─────────────────────────────────────────────────────────────────

    public async Task<MeasurementReading> RecordReadingAsync(
        string measurementId,
        string periodLabel,
        DateTime periodStart,
        decimal value,
        string? notes,
        int enteredById)
    {
        var measurement = await _context.ImprovementMeasurements
            .Include(m => m.Improvement)
            .FirstOrDefaultAsync(m => m.Id == measurementId)
            ?? throw new KeyNotFoundException($"Measurement {measurementId} not found.");

        // Guard against duplicate entry for the same period — the unique
        // index will also catch this at the DB layer, but we want a clean
        // friendly exception.
        var existing = await _context.MeasurementReadings
            .FirstOrDefaultAsync(r => r.MeasurementId == measurementId && r.PeriodLabel == periodLabel);
        if (existing != null)
        {
            // Update in place rather than throwing — users often re-enter to correct typos.
            existing.Value = value;
            existing.Notes = notes;
            existing.EnteredById = enteredById;
            existing.EnteredAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return existing;
        }

        var reading = new MeasurementReading
        {
            Id = Guid.NewGuid().ToString(),
            MeasurementId = measurementId,
            PeriodLabel = periodLabel,
            PeriodStart = periodStart,
            Value = value,
            Notes = notes,
            EnteredById = enteredById,
            EnteredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _context.MeasurementReadings.Add(reading);

        // Roll up: update the parent measurement's ToBeValue so dashboards
        // and reports stay in sync with the latest collected value. This is
        // a soft projection of "what we're trending toward" — it is NOT the
        // final post-closure figure (that's captured in the closure report).
        measurement.ToBeValue = value;
        measurement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reading recorded for measurement {MeasurementId} period {Period} = {Value}",
            measurementId, periodLabel, value);

        // Notify the initiative owner if somebody else entered the reading
        if (measurement.Improvement != null &&
            int.TryParse(measurement.Improvement.OwnerId, out var ownerId) &&
            ownerId != enteredById)
        {
            try
            {
                await _notificationService.SendAsync(ownerId,
                    "New measurement reading recorded",
                    "تم تسجيل قراءة جديدة",
                    $"Someone recorded a new reading ({value} {measurement.UnitOfMeasure}) for '{measurement.NameEn}' on initiative '{measurement.Improvement.TitleEn}'.",
                    $"تم تسجيل قراءة جديدة ({value} {measurement.UnitOfMeasure}) للمقياس '{measurement.NameAr}' في المبادرة '{measurement.Improvement.TitleAr}'.",
                    "Info",
                    measurement.ImprovementId,
                    "Improvement",
                    $"/Improvements/Details/{measurement.ImprovementId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify owner of new measurement reading");
            }
        }

        return reading;
    }

    // ─────────────────────────────────────────────────────────────────
    // Trend history
    // ─────────────────────────────────────────────────────────────────

    public async Task<List<MeasurementReading>> GetTrendAsync(string measurementId)
    {
        return await _context.MeasurementReadings
            .AsNoTracking()
            .Where(r => r.MeasurementId == measurementId)
            .OrderBy(r => r.PeriodStart)
            .ToListAsync();
    }
}
