using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.ServiceManagement;

namespace ESEMS.Web.Models.SLA;

/// <summary>
/// Represents an SLA breach incident (ISO 20000-1:2018)
/// </summary>
public class SLABreach : BilingualEntity
{
    /// <summary>
    /// Breach number (auto-generated, e.g., SLAB-2026-0001)
    /// </summary>
    public string BreachNumber { get; set; } = string.Empty;

    /// <summary>
    /// SLA definition ID
    /// </summary>
    public string SLADefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Related incident ID (if applicable)
    /// </summary>
    public string? IncidentId { get; set; }

    /// <summary>
    /// Breach date/time
    /// </summary>
    public DateTime BreachDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Target value (from SLA definition)
    /// </summary>
    public decimal TargetValue { get; set; }

    /// <summary>
    /// Actual value achieved
    /// </summary>
    public decimal ActualValue { get; set; }

    /// <summary>
    /// Variance (ActualValue - TargetValue)
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Variance percentage
    /// </summary>
    public decimal VariancePercentage { get; set; }

    /// <summary>
    /// Severity (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    [Range(1, 4)]
    public int Severity { get; set; } = 3;

    /// <summary>
    /// Root cause
    /// </summary>
    public string? RootCause { get; set; }

    /// <summary>
    /// Corrective action taken
    /// </summary>
    public string? CorrectiveAction { get; set; }

    /// <summary>
    /// Preventive action taken
    /// </summary>
    public string? PreventiveAction { get; set; }

    /// <summary>
    /// Responsible user ID
    /// </summary>
    public string? ResponsibleUserId { get; set; }

    /// <summary>
    /// Acknowledged date
    /// </summary>
    public DateTime? AcknowledgedDate { get; set; }

    /// <summary>
    /// Resolved date
    /// </summary>
    public DateTime? ResolvedDate { get; set; }

    /// <summary>
    /// Is resolved
    /// </summary>
    public bool IsResolved { get; set; } = false;

    /// <summary>
    /// Financial impact
    /// </summary>
    public decimal? FinancialImpact { get; set; }

    /// <summary>
    /// Customer impact description
    /// </summary>
    public string? CustomerImpact { get; set; }

    // Navigation properties
    public SLADefinition? SLADefinition { get; set; }
    public Incident? Incident { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? ResponsibleUser { get; set; }

    /// <summary>
    /// Calculate variance
    /// </summary>
    public void CalculateVariance()
    {
        Variance = ActualValue - TargetValue;
        if (TargetValue != 0)
        {
            VariancePercentage = (Variance / TargetValue) * 100;
        }
    }

    /// <summary>
    /// Determine severity based on variance percentage
    /// </summary>
    public void DetermineSeverity()
    {
        var absVariancePercentage = Math.Abs(VariancePercentage);
        Severity = absVariancePercentage switch
        {
            >= 50 => 1,  // Critical
            >= 25 => 2,  // High
            >= 10 => 3,  // Medium
            _ => 4       // Low
        };
    }
}

