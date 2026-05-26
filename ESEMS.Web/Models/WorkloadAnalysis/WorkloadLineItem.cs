using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Services;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.WorkloadAnalysis;

/// <summary>
/// One row in a workload scenario — represents a single process or service
/// with its annual volume, processing time, and optional complexity weighting.
/// </summary>
public class WorkloadLineItem : AuditableBilingualEntity
{
    /// <summary>FK to the parent scenario</summary>
    public string WorkloadScenarioId { get; set; } = string.Empty;

    /// <summary>Optional link to an existing APQC Process</summary>
    public string? ProcessId { get; set; }

    /// <summary>Optional link to an existing Service</summary>
    public string? ServiceId { get; set; }

    /// <summary>Number of transactions/requests per year</summary>
    [Range(0, int.MaxValue)]
    public int AnnualVolume { get; set; }

    /// <summary>Average hands-on processing time per transaction in minutes</summary>
    [Range(typeof(decimal), "0", "100000", ParseLimitsInInvariantCulture = true)]
    public decimal AvgProcessingTimeMinutes { get; set; }

    // ── Complexity weighting (optional) ──

    public bool ComplexityEnabled { get; set; }

    /// <summary>Percentage of volume that is simple (0-100)</summary>
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal? SimpleVolumePercent { get; set; } = 100m;

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal? MediumVolumePercent { get; set; }

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal? ComplexVolumePercent { get; set; }

    /// <summary>Time multiplier for simple transactions</summary>
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal? SimpleMult { get; set; } = 1.0m;

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal? MediumMult { get; set; } = 1.5m;

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal? ComplexMult { get; set; } = 2.5m;

    /// <summary>JSON array of 12 decimal values — monthly volume distribution percentages</summary>
    public string? SeasonalDistribution { get; set; }

    public string? Notes { get; set; }

    // ── Navigation ──
    public WorkloadScenario? Scenario { get; set; }
    public Process? Process { get; set; }
    public Service? Service { get; set; }

    // ── Computed (not mapped) ──

    /// <summary>
    /// Volume adjusted by complexity weights.
    /// If complexity is disabled, equals AnnualVolume.
    /// </summary>
    [NotMapped]
    public decimal WeightedVolume
    {
        get
        {
            if (!ComplexityEnabled)
                return AnnualVolume;

            var simple  = AnnualVolume * (SimpleVolumePercent  ?? 0) / 100m * (SimpleMult  ?? 1.0m);
            var medium  = AnnualVolume * (MediumVolumePercent  ?? 0) / 100m * (MediumMult  ?? 1.5m);
            var complex = AnnualVolume * (ComplexVolumePercent ?? 0) / 100m * (ComplexMult ?? 2.5m);
            return simple + medium + complex;
        }
    }

    /// <summary>Total workload hours = weighted volume * processing time / 60</summary>
    [NotMapped]
    public decimal WorkloadHours => WeightedVolume * AvgProcessingTimeMinutes / 60m;

    /// <summary>FTE for this line item alone (requires scenario config)</summary>
    [NotMapped]
    public decimal RequiredFTE =>
        Scenario?.Config != null && Scenario.Config.NetAvailableHoursPerFTE > 0
            ? WorkloadHours / Scenario.Config.NetAvailableHoursPerFTE
            : 0m;
}
