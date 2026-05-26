using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.RiskManagement;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// Base class for measurements/KPIs
/// </summary>
public abstract class MeasurementBase : AuditableBilingualEntity
{
    /// <summary>
    /// Measurement code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public string? UnitOfMeasure { get; set; }

    /// <summary>
    /// Target value
    /// </summary>
    public decimal? TargetValue { get; set; }

    /// <summary>
    /// Current/actual value
    /// </summary>
    public decimal? ActualValue { get; set; }

    /// <summary>
    /// Minimum acceptable value
    /// </summary>
    public decimal? MinValue { get; set; }

    /// <summary>
    /// Maximum acceptable value
    /// </summary>
    public decimal? MaxValue { get; set; }

    /// <summary>
    /// Measurement frequency (e.g., Daily, Weekly, Monthly)
    /// </summary>
    public string? Frequency { get; set; }

    /// <summary>
    /// Data source for the measurement
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Whether this measurement is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets the performance percentage
    /// </summary>
    public decimal? GetPerformancePercentage()
    {
        if (!TargetValue.HasValue || TargetValue.Value == 0 || !ActualValue.HasValue)
            return null;
        return (ActualValue.Value / TargetValue.Value) * 100;
    }
}

/// <summary>
/// Measurement for a Service
/// </summary>
public class ServiceMeasurement : MeasurementBase
{
    public string ServiceId { get; set; } = string.Empty;
    public Service? Service { get; set; }
}

/// <summary>
/// Measurement for a Process
/// </summary>
public class ProcessMeasurement : MeasurementBase
{
    public string ProcessId { get; set; } = string.Empty;
    public Process? Process { get; set; }
}

/// <summary>
/// Risk associated with a Process
/// </summary>
public class ProcessRisk : AuditableBilingualEntity
{
    public string ProcessId { get; set; } = string.Empty;
    
    /// <summary>
    /// Risk code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Risk category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional link to Enterprise Risk register (ISO 31000:2018)
    /// </summary>
    public string? EnterpriseRiskId { get; set; }

    /// <summary>
    /// Likelihood score (1-5)
    /// </summary>
    [Range(1, 5)]
    public int LikelihoodScore { get; set; } = 1;

    /// <summary>
    /// Impact score (1-5)
    /// </summary>
    [Range(1, 5)]
    public int ImpactScore { get; set; } = 1;

    /// <summary>
    /// Calculated risk score (Likelihood x Impact)
    /// </summary>
    public int RiskScore => LikelihoodScore * ImpactScore;

    /// <summary>
    /// Mitigation strategy
    /// </summary>
    public string? MitigationStrategy { get; set; }

    /// <summary>
    /// Risk owner (User ID)
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// Whether this risk is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Process? Process { get; set; }
    public EnterpriseRisk? EnterpriseRisk { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? Owner { get; set; }

    /// <summary>
    /// Gets the risk level based on score
    /// </summary>
    public string GetRiskLevel()
    {
        return RiskScore switch
        {
            <= 4 => "Low",
            <= 9 => "Medium",
            <= 15 => "High",
            _ => "Critical"
        };
    }
}

