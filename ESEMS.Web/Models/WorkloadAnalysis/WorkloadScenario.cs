using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.WorkloadAnalysis;

/// <summary>
/// A saved workload analysis run. Each scenario belongs to an org unit,
/// references a <see cref="WorkloadConfig"/>, and contains one or more
/// <see cref="WorkloadLineItem"/> rows — one per process/service analyzed.
/// </summary>
public class WorkloadScenario : AuditableBilingualEntity, IOwnedByUnit
{
    /// <summary>Auto-generated code: WS-001, WS-002, …</summary>
    public string Code { get; set; } = string.Empty;

    public WorkloadScenarioStatus Status { get; set; } = WorkloadScenarioStatus.Draft;

    /// <summary>The fiscal/calendar year this analysis covers</summary>
    public int FiscalYear { get; set; } = DateTime.UtcNow.Year;

    /// <summary>Scoping FK (IOwnedByUnit)</summary>
    public int? OwningUnitId { get; set; }

    /// <summary>FK to the configuration parameters used for calculation</summary>
    public string WorkloadConfigId { get; set; } = string.Empty;

    /// <summary>Annual volume growth rate for projections (e.g. 10 = 10%)</summary>
    public decimal? GrowthRatePercent { get; set; }

    /// <summary>How many years to project forward (0 = current year only)</summary>
    public int ProjectionYears { get; set; }

    /// <summary>Actual current staff count for gap analysis. Soft cap at 10,000:
    /// the largest realistic Dubai-gov org has ~2,000 staff, so 10k gives 5×
    /// headroom while catching typos like 99,999 that otherwise blow up the
    /// gap chart with a -99,000 sized bar.</summary>
    [System.ComponentModel.DataAnnotations.Range(0, 10000)]
    public int? CurrentHeadcount { get; set; }

    public string? Notes { get; set; }

    // ── Navigation ──
    public WorkloadConfig? Config { get; set; }
    public OrganizationUnit? OwningUnit { get; set; }
    public ICollection<WorkloadLineItem> LineItems { get; set; } = new List<WorkloadLineItem>();

    // ── Computed (not mapped) ──

    [NotMapped]
    public decimal TotalWorkloadHours =>
        LineItems?.Sum(li => li.WorkloadHours) ?? 0m;

    [NotMapped]
    public decimal TotalRequiredFTE =>
        Config != null && Config.NetAvailableHoursPerFTE > 0
            ? TotalWorkloadHours / Config.NetAvailableHoursPerFTE
            : 0m;

    /// <summary>FTE adjusted for volume growth across projection years</summary>
    [NotMapped]
    public decimal AdjustedFTE
    {
        get
        {
            if (ProjectionYears <= 0 || !GrowthRatePercent.HasValue || GrowthRatePercent.Value == 0)
                return TotalRequiredFTE;
            var growthFactor = (decimal)Math.Pow((double)(1m + GrowthRatePercent.Value / 100m), ProjectionYears);
            return TotalRequiredFTE * growthFactor;
        }
    }

    [NotMapped]
    public decimal SupervisoryFTE =>
        Config != null && Config.SupervisoryRatio > 0
            ? AdjustedFTE / Config.SupervisoryRatio
            : 0m;

    [NotMapped]
    public decimal TotalFTE => Math.Ceiling(AdjustedFTE + SupervisoryFTE);

    /// <summary>Positive = understaffed, negative = overstaffed</summary>
    [NotMapped]
    public decimal FTEGap => TotalFTE - (CurrentHeadcount ?? 0);
}
