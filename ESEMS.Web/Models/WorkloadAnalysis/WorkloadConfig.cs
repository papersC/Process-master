using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.APQC;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.WorkloadAnalysis;

/// <summary>
/// Entity-level parameters for FTE/workload calculations.
/// One global record (OrganizationUnitId = null) plus optional per-unit overrides.
/// UAE/Dubai government defaults are baked in.
/// </summary>
public class WorkloadConfig : AuditableBilingualEntity, IOrganizationScoped
{
    /// <summary>Standard working hours per day (UAE gov: 7.5)</summary>
    [Range(typeof(decimal), "0.5", "24", ParseLimitsInInvariantCulture = true)]
    public decimal WorkingHoursPerDay { get; set; } = 7.5m;

    /// <summary>Working days per week (UAE: 5, Mon-Fri)</summary>
    [Range(1, 7)]
    public int WorkingDaysPerWeek { get; set; } = 5;

    /// <summary>Public holidays per year (UAE: ~12)</summary>
    [Range(0, 365)]
    public int PublicHolidaysPerYear { get; set; } = 12;

    /// <summary>Annual leave entitlement in working days (UAE: 22)</summary>
    [Range(0, 365)]
    public int AnnualLeaveDays { get; set; } = 22;

    /// <summary>Average sick leave usage in days</summary>
    [Range(0, 365)]
    public int AverageSickDays { get; set; } = 7;

    /// <summary>Training/development days per year (FAHR mandate)</summary>
    [Range(0, 365)]
    public int TrainingDaysPerYear { get; set; } = 7;

    /// <summary>Percentage of time spent on admin, meetings, email (0-100)</summary>
    [Range(typeof(decimal), "0", "99", ParseLimitsInInvariantCulture = true)]
    public decimal AdminOverheadPercent { get; set; } = 15m;

    /// <summary>Target employee utilization rate (0.0 – 1.0)</summary>
    [Range(typeof(decimal), "0.01", "1.0", ParseLimitsInInvariantCulture = true)]
    public decimal TargetUtilizationRate { get; set; } = 0.80m;

    /// <summary>Number of direct reports per supervisor (1:N)</summary>
    [Range(1, 100)]
    public int SupervisoryRatio { get; set; } = 8;

    /// <summary>FK to OrganizationUnit — null means global default config</summary>
    public int? OrganizationUnitId { get; set; }

    /// <summary>Month number when the fiscal year starts (1 = January)</summary>
    [Range(1, 12)]
    public int FiscalYearStart { get; set; } = 1;

    // ── Navigation ──
    public OrganizationUnit? OrganizationUnit { get; set; }

    // ── Computed (not mapped to DB) ──

    [NotMapped]
    public int GrossWorkingDaysPerYear =>
        (WorkingDaysPerWeek * 52) - PublicHolidaysPerYear;

    [NotMapped]
    public int AbsenceDays =>
        AnnualLeaveDays + AverageSickDays + TrainingDaysPerYear;

    [NotMapped]
    public int NetWorkingDaysPerYear =>
        GrossWorkingDaysPerYear - AbsenceDays;

    [NotMapped]
    public decimal GrossAnnualHours =>
        GrossWorkingDaysPerYear * WorkingHoursPerDay;

    /// <summary>
    /// The key number: how many productive hours one FTE delivers per year,
    /// after leave, holidays, training, sick days, admin overhead, and
    /// utilization adjustment.
    /// </summary>
    [NotMapped]
    public decimal NetAvailableHoursPerFTE =>
        NetWorkingDaysPerYear
        * WorkingHoursPerDay
        * (1m - AdminOverheadPercent / 100m)
        * TargetUtilizationRate;

    [NotMapped]
    public decimal AllowanceFactor =>
        GrossAnnualHours > 0 ? NetAvailableHoursPerFTE / GrossAnnualHours : 0;
}
