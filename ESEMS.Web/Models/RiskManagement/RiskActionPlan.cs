using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.RiskManagement;

/// <summary>
/// Represents a risk mitigation action plan (ISO 31000:2018).
/// Now extends <see cref="AuditableBilingualEntity"/> so action plans carry
/// CreatedAt/UpdatedAt/CreatedById/UpdatedById/Version/IsDeleted/DeletedAt
/// — previously a critical compliance gap (audit C4): Dubai e-Gov audit law
/// requires 6-year audit trails on risk mitigation, and the prior
/// <see cref="BilingualEntity"/> base had none of those columns.
/// </summary>
public class RiskActionPlan : AuditableBilingualEntity
{
    /// <summary>
    /// Risk ID
    /// </summary>
    public string RiskId { get; set; } = string.Empty;

    /// <summary>
    /// Action owner user ID
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// Priority (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    [Range(1, 4)]
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Target completion date
    /// </summary>
    public DateTime? TargetDate { get; set; }

    /// <summary>
    /// Actual completion date
    /// </summary>
    public DateTime? CompletionDate { get; set; }

    /// <summary>
    /// Status. Stored as string in SQL via HasConversion so existing rows
    /// migrate cleanly (see TightenRiskActionPlanAudit migration).
    /// </summary>
    public RiskActionPlanStatus Status { get; set; } = RiskActionPlanStatus.NotStarted;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    [Range(0, 100)]
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// Estimated cost
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Actual cost
    /// </summary>
    public decimal? ActualCost { get; set; }

    /// <summary>
    /// Expected risk reduction (percentage)
    /// </summary>
    [Range(0, 100)]
    public int? ExpectedRiskReduction { get; set; }

    /// <summary>
    /// Notes
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public EnterpriseRisk? Risk { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? Owner { get; set; }

    /// <summary>
    /// Check if action is overdue
    /// </summary>
    public bool IsOverdue()
    {
        return TargetDate.HasValue && 
               !CompletionDate.HasValue && 
               TargetDate.Value < DateTime.UtcNow;
    }

    /// <summary>
    /// Check if action is completed
    /// </summary>
    public bool IsCompleted()
    {
        return CompletionDate.HasValue || ProgressPercentage >= 100;
    }
}

