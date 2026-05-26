using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Services.Improvements;

/// <summary>
/// Audit #12 — Benefits aggregation from the time-series MeasurementReadings.
///
/// Replaces the historical practice of treating
/// <c>ImprovementInitiative.ActualCostSavings</c> and
/// <c>ImprovementInitiative.ActualTimeSavings</c> as a single flat snapshot
/// captured at closure. With this service, those flat fields stay as the
/// "snapshot at closure" but the truth lives in the time-series readings:
///
///   - Sums every <c>MeasurementReading.Value</c> for measurements that are
///     <c>IsBenefitTracked = true</c>, grouped by Cost vs Time.
///   - Optional period filter so the post-closure benefits-realisation views
///     can show "actual savings observed in the 0-3M / 3-6M / 6-12M windows
///     since closure".
///
/// Returning a single struct keeps callers simple — no extra DbContext lookup
/// per benefit type.
/// </summary>
public class BenefitsAggregationService
{
    private readonly ApplicationDbContext _db;

    public BenefitsAggregationService(ApplicationDbContext db) => _db = db;

    public readonly record struct BenefitsTotals(
        decimal CostSavings,
        decimal TimeSavingsHours,
        int CostReadingCount,
        int TimeReadingCount);

    /// <summary>
    /// Sums all readings for benefit-tracked measurements on the given
    /// initiative, optionally constrained to a time window. Pass nulls to
    /// aggregate the full lifetime.
    /// </summary>
    public async Task<BenefitsTotals> GetTotalsAsync(
        string improvementId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        var readings = await _db.Set<Models.Improvement.MeasurementReading>()
            .AsNoTracking()
            .Join(_db.ImprovementMeasurements.AsNoTracking(),
                  r => r.MeasurementId,
                  m => m.Id,
                  (r, m) => new { Reading = r, Measurement = m })
            .Where(x => x.Measurement.ImprovementId == improvementId
                        && !x.Measurement.IsDeleted
                        && x.Measurement.IsBenefitTracked
                        && x.Reading.Value.HasValue
                        && (from == null || x.Reading.PeriodStart >= from)
                        && (to == null || x.Reading.PeriodStart < to))
            .Select(x => new { x.Measurement.MeasurementType, x.Reading.Value })
            .ToListAsync(ct);

        var costRows = readings.Where(x => x.MeasurementType == ImprovementMeasurementType.Cost).ToList();
        var timeRows = readings.Where(x => x.MeasurementType == ImprovementMeasurementType.Time).ToList();

        return new BenefitsTotals(
            CostSavings:       costRows.Sum(x => x.Value!.Value),
            TimeSavingsHours:  timeRows.Sum(x => x.Value!.Value),
            CostReadingCount:  costRows.Count,
            TimeReadingCount:  timeRows.Count);
    }
}
