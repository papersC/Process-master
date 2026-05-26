using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Measurement definition for an improvement initiative (RFP Section 15).
/// Tracks As-Is, Target, and To-Be values for each measurement.
/// </summary>
public class ImprovementMeasurement : AuditableBilingualEntity
{
    /// <summary>
    /// Parent improvement initiative ID
    /// </summary>
    public string ImprovementId { get; set; } = string.Empty;

    /// <summary>
    /// Type of measurement (Satisfaction, Cost, Time, Productivity, etc.)
    /// </summary>
    public ImprovementMeasurementType MeasurementType { get; set; } = ImprovementMeasurementType.Custom;

    /// <summary>
    /// Unit of measure (e.g., %, AED, minutes, count)
    /// </summary>
    public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>
    /// Target value to achieve
    /// </summary>
    public decimal? TargetValue { get; set; }

    /// <summary>
    /// Current (As-Is) measurement value
    /// </summary>
    public decimal? AsIsValue { get; set; }

    /// <summary>
    /// Projected (To-Be) measurement value after improvement
    /// </summary>
    public decimal? ToBeValue { get; set; }

    /// <summary>
    /// Weight for prioritization (0-100, total per initiative should equal 100)
    /// </summary>
    public int Weight { get; set; } = 0;

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether this measurement is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Process this measurement scopes to (audit #13). Replaces the old
    /// magic-string <c>AppliesTo = "process:{id}"</c> with a real FK.
    /// Mutually exclusive with <see cref="AppliesToServiceId"/> — exactly
    /// one (or neither) should be non-null per row.
    /// </summary>
    public string? AppliesToProcessId { get; set; }

    /// <summary>
    /// Service this measurement scopes to (audit #13). Replaces the old
    /// magic-string <c>AppliesTo = "service:{id}"</c> with a real FK.
    /// Mutually exclusive with <see cref="AppliesToProcessId"/>.
    /// </summary>
    public string? AppliesToServiceId { get; set; }

    /// <summary>
    /// Measuring period / frequency (e.g. Daily, Weekly, Monthly, Quarterly, Annually).
    /// Defaults to "Monthly" so MeasurementReminderHostedService has a cadence
    /// to use even if the wizard / Add Measurement modal didn't capture one.
    /// Without this, a measurement with null period would never produce
    /// reminder notifications to the owner.
    /// </summary>
    public string? MeasuringPeriod { get; set; } = "Monthly";

    /// <summary>
    /// Measuring method (e.g. Survey, System Report, Manual Count, Automated)
    /// </summary>
    public string? MeasuringMethod { get; set; }

    /// <summary>
    /// BPMN reference (process diagram activity or element reference)
    /// </summary>
    public string? BpmnReference { get; set; }

    /// <summary>
    /// Audit #15: optional FK into the central <see cref="KpiDefinition"/>
    /// catalog. When set, dashboards group by KPI definition (canonical
    /// name + unit) instead of by per-measurement free text. Nullable to
    /// preserve historical rows that pre-date the catalog.
    /// </summary>
    public string? KpiDefinitionId { get; set; }

    /// <summary>
    /// Audit #12: marks this measurement as a tracked benefit so the closure
    /// report and the post-closure benefits-realization view aggregate it
    /// into the running ActualCostSavings / ActualTimeSavings totals
    /// (replacing the historical practice of capturing actuals as flat fields
    /// frozen at closure time). Combine with <see cref="MeasurementType"/>
    /// = Cost / Time to specify which actual it contributes to.
    /// </summary>
    public bool IsBenefitTracked { get; set; } = false;

    /// <summary>
    /// Priority ranking: H1 = Critical, H2 = Important, H3 = Standard.
    /// Historical field — the wizard now derives priority from the
    /// initiative-level Impact × Effort quadrant instead of from each
    /// measurement. Kept nullable for backwards compatibility.
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Direction of improvement: HigherBetter (e.g. satisfaction %, throughput)
    /// or LowerBetter (e.g. processing time, error rate). Type-safe enum
    /// (audit #19); persisted as nvarchar via a value converter so existing
    /// "HigherBetter"/"LowerBetter" rows keep round-tripping.
    /// Used by <see cref="GetImprovementPercentage"/> so dashboards can colour
    /// a reduction in processing time as a positive improvement.
    /// Nullable for rows written before this column existed — those default
    /// to HigherBetter in the calculation.
    /// </summary>
    public MeasurementDirection? Direction { get; set; }

    // Navigation property
    public ImprovementInitiative? Improvement { get; set; }
    public APQC.Process? AppliesToProcess { get; set; }
    public Services.Service? AppliesToService { get; set; }
    public KpiDefinition? KpiDefinition { get; set; }

    /// <summary>
    /// Calculates the improvement percentage from As-Is to To-Be, respecting
    /// the measurement's direction. A positive result always means
    /// "improvement", regardless of whether the metric goes up or down.
    /// </summary>
    public decimal? GetImprovementPercentage()
    {
        if (!AsIsValue.HasValue || AsIsValue.Value == 0 || !ToBeValue.HasValue)
            return null;

        var rawDelta = (ToBeValue.Value - AsIsValue.Value) / Math.Abs(AsIsValue.Value) * 100m;

        // LowerBetter flips the sign so a drop in processing time shows
        // as a positive improvement percentage.
        return Direction == MeasurementDirection.LowerBetter ? -rawDelta : rawDelta;
    }

    /// <summary>
    /// Calculates the target achievement percentage as the share of the
    /// baseline→target journey that has been traveled.
    ///
    /// Formula: (current - baseline) / (target - baseline), clamped to
    /// [0, 100], direction-agnostic. Both LowerBetter and HigherBetter
    /// fall out naturally because the numerator and denominator share a
    /// sign — improving toward target gives a positive ratio either way.
    ///
    /// Why not (current / target)? Because that ignores the starting
    /// point. A satisfaction metric that started at 82, targets 95, and
    /// reads 87 is honestly 5/13 = 38% of the way there — not 92%
    /// (the prior formula's answer). The old formula was also broken
    /// for LowerBetter — a time metric falling 30→25 toward 20 would
    /// show 125%, not the actual 50% progress.
    ///
    /// Returns null when baseline / target / current isn't set, or when
    /// baseline equals target (no journey to measure).
    /// </summary>
    public decimal? GetTargetAchievementPercentage()
    {
        if (!TargetValue.HasValue || !ToBeValue.HasValue || !AsIsValue.HasValue)
            return null;
        var distance = TargetValue.Value - AsIsValue.Value;
        if (distance == 0) return ToBeValue.Value == TargetValue.Value ? 100m : 0m;
        var traveled = ToBeValue.Value - AsIsValue.Value;
        var pct = traveled / distance * 100m;
        // Cap at 100 (over-achievement) and 0 (regression / no progress).
        return Math.Max(0m, Math.Min(100m, pct));
    }

    /// <summary>
    /// Gets a friendly label for the measurement type
    /// </summary>
    public string GetTypeLabel()
    {
        return MeasurementType switch
        {
            ImprovementMeasurementType.Satisfaction => "Satisfaction",
            ImprovementMeasurementType.Cost => "Cost",
            ImprovementMeasurementType.Time => "Time",
            ImprovementMeasurementType.Productivity => "Productivity",
            ImprovementMeasurementType.Capacity => "Capacity",
            ImprovementMeasurementType.NumberOfVisits => "Number of Visits",
            ImprovementMeasurementType.NumberOfDocuments => "Number of Documents",
            ImprovementMeasurementType.Custom => "Custom",
            _ => "Custom"
        };
    }
}

