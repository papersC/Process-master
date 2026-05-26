using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.SLA;

/// <summary>
/// Represents a Service Level Agreement definition (ISO 20000-1:2018)
/// </summary>
public class SLADefinition : AuditableBilingualEntity
{
    /// <summary>
    /// SLA code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Related service ID
    /// </summary>
    public string? ServiceId { get; set; }

    /// <summary>
    /// Metric name (e.g., "Response Time", "Resolution Time", "Availability")
    /// </summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Target value
    /// </summary>
    public decimal TargetValue { get; set; }

    /// <summary>
    /// Unit of measurement (e.g., "hours", "minutes", "percentage")
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Threshold for warning (percentage of target)
    /// </summary>
    public decimal? WarningThreshold { get; set; }

    /// <summary>
    /// Measurement frequency (e.g., "Daily", "Weekly", "Monthly")
    /// </summary>
    public string MeasurementFrequency { get; set; } = "Daily";

    /// <summary>
    /// Calculation method
    /// </summary>
    public string? CalculationMethod { get; set; }

    /// <summary>
    /// Responsible organization unit ID
    /// </summary>
    public int? ResponsibleUnitId { get; set; }

    /// <summary>
    /// Effective from date
    /// </summary>
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Effective to date
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Penalty for breach
    /// </summary>
    public string? PenaltyForBreach { get; set; }

    /// <summary>
    /// Escalation procedure
    /// </summary>
    public string? EscalationProcedure { get; set; }

    // Navigation properties
    public Service? Service { get; set; }
    public OrganizationUnit? ResponsibleUnit { get; set; }
    public ICollection<SLABreach> Breaches { get; set; } = new List<SLABreach>();

    /// <summary>
    /// Check if SLA is currently effective
    /// </summary>
    public bool IsEffective()
    {
        var now = DateTime.UtcNow;
        return IsActive && 
               now >= EffectiveFrom && 
               (!EffectiveTo.HasValue || now <= EffectiveTo.Value);
    }
}

