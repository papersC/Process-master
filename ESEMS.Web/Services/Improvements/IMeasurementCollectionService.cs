using ESEMS.Web.Models.Improvement;

namespace ESEMS.Web.Services.Improvements;

/// <summary>
/// Cadence-aware collection layer for <see cref="ImprovementMeasurement"/>.
///
/// Responsible for three things:
/// 1. Computing which measurements are "due" for a reading given their
///    <c>MeasuringPeriod</c> (Daily / Weekly / Monthly / Quarterly / Annually)
///    and the date of the last reading.
/// 2. Recording new <see cref="MeasurementReading"/> entries and
///    rolling them up into the parent measurement's aggregate values.
/// 3. Projecting the raw time series for the trend chart on the
///    Measurements tab.
/// </summary>
public interface IMeasurementCollectionService
{
    /// <summary>
    /// All measurements that belong to active initiatives owned by
    /// <paramref name="userId"/> and whose most recent reading is older
    /// than the start of the current period (or who have no readings at
    /// all). Used by the reminder background job and by the Details page
    /// "Due now" banner.
    /// </summary>
    Task<List<DueReading>> GetDueReadingsAsync(int userId);

    /// <summary>
    /// Persist a new reading. Throws if a reading already exists for
    /// (measurementId, periodLabel). Raises an "initiative updated"
    /// notification if the reading is the first on-target data point.
    /// </summary>
    Task<MeasurementReading> RecordReadingAsync(
        string measurementId,
        string periodLabel,
        DateTime periodStart,
        decimal value,
        string? notes,
        int enteredById);

    /// <summary>
    /// Full history of readings for one measurement, oldest first,
    /// ready for Chart.js consumption on the Measurements tab.
    /// </summary>
    Task<List<MeasurementReading>> GetTrendAsync(string measurementId);

    /// <summary>
    /// Given a measurement and an arbitrary point in time, returns the
    /// canonical PeriodLabel + PeriodStart for the period that contains
    /// <paramref name="at"/>. Exposed so the UI can display the "current
    /// period" label without re-implementing the cadence logic.
    /// </summary>
    (string PeriodLabel, DateTime PeriodStart) PeriodFor(ImprovementMeasurement measurement, DateTime at);
}

/// <summary>
/// Lightweight projection of a due reading for UI display and notification.
/// </summary>
public record DueReading(
    string MeasurementId,
    string InitiativeId,
    string InitiativeTitleEn,
    string InitiativeTitleAr,
    string MeasurementNameEn,
    string MeasurementNameAr,
    string Unit,
    string PeriodLabel,
    DateTime PeriodStart,
    string MeasuringPeriod
);
