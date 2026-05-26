using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// A single actual-value reading collected for an
/// <see cref="ImprovementMeasurement"/> during execution.
///
/// A measurement defines its collection cadence via
/// <c>MeasuringPeriod</c> (Daily / Weekly / Monthly / Quarterly / Annually).
/// For every period that elapses while the parent initiative is in an
/// executing state (InProgress / OnHold), the system prompts the owner
/// to enter an actual value. Each entry becomes one MeasurementReading
/// row, and the full history feeds the trend chart on the Measurements
/// tab and drives the "% to target" progress bar.
///
/// Unlike the classic As-Is → Target → To-Be triplet on the parent
/// measurement, these readings are a time series — they are the raw
/// data points that let the owner see whether the initiative is on
/// track between review dates.
/// </summary>
public class MeasurementReading
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Foreign key to the parent <see cref="ImprovementMeasurement"/>.
    /// </summary>
    [Required]
    public string MeasurementId { get; set; } = string.Empty;

    /// <summary>
    /// Label of the period this reading covers. The format depends on
    /// <c>MeasurementMeasuringPeriod</c>:
    ///   Daily      → "yyyy-MM-dd"
    ///   Weekly     → "yyyy-Www"  (ISO week)
    ///   Monthly    → "yyyy-MM"
    ///   Quarterly  → "yyyy-Qq"
    ///   Annually   → "yyyy"
    /// We store it as a string so the DB can enforce one-reading-per-
    /// period via a unique index on (MeasurementId, PeriodLabel).
    /// </summary>
    [Required, MaxLength(30)]
    public string PeriodLabel { get; set; } = string.Empty;

    /// <summary>
    /// Canonical UTC start-of-period timestamp, used for chart ordering
    /// and for detecting which periods are due without parsing PeriodLabel.
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// The recorded value. Nullable so a user can pre-create an empty
    /// row when they want to acknowledge a period but haven't collected
    /// the number yet.
    /// </summary>
    public decimal? Value { get; set; }

    /// <summary>
    /// Free-form note the user attached when entering the reading (e.g.
    /// "Q3 spike due to loan-portfolio review").
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// UserId of whoever entered this reading. Null when the row is
    /// system-created as a placeholder by the reminder background service.
    /// </summary>
    public int? EnteredById { get; set; }

    /// <summary>
    /// When the value was actually entered (null for placeholders).
    /// </summary>
    public DateTime? EnteredAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(MeasurementId))]
    public ImprovementMeasurement? Measurement { get; set; }
}
