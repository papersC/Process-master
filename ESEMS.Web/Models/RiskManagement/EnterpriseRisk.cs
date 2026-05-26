using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.Improvement;

namespace ESEMS.Web.Models.RiskManagement;

/// <summary>
/// Represents an enterprise risk in the Risk Management system (ISO 31000:2018)
/// </summary>
public class EnterpriseRisk : AuditableBilingualEntity, Common.IOrganizationScoped
{
    /// <summary>
    /// Risk number (auto-generated, e.g., RISK-2026-0001)
    /// </summary>
    public string RiskNumber { get; set; } = string.Empty;

    /// <summary>
    /// Risk category ID
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Related process ID
    /// </summary>
    public string? ProcessId { get; set; }

    /// <summary>
    /// Related organization unit ID
    /// </summary>
    public int? OrganizationUnitId { get; set; }

    /// <summary>
    /// Risk owner user ID
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// Likelihood (1=Rare, 2=Unlikely, 3=Possible, 4=Likely, 5=Almost Certain).
    /// [Range] is enforced server-side via ModelState — protects against
    /// direct POSTs that bypass the form's 5-option select. Without this,
    /// a smuggled Likelihood=99 would yield InherentRiskScore=99×Impact and
    /// land in the heat map at off-the-chart coordinates.
    /// </summary>
    [Range(1, 5)]
    public int Likelihood { get; set; } = 3;

    /// <summary>
    /// Impact (1=Insignificant, 2=Minor, 3=Moderate, 4=Major, 5=Catastrophic).
    /// Same direct-POST defense as Likelihood.
    /// </summary>
    [Range(1, 5)]
    public int Impact { get; set; } = 3;

    /// <summary>
    /// Inherent risk score (Likelihood × Impact). DATA-002 — bounded so a
    /// direct POST can't set 999 / -50 / etc. Max is 5×5 = 25; 0 reserved
    /// for "not yet calculated".
    /// </summary>
    [Range(0, 25)]
    public int InherentRiskScore { get; set; }

    /// <summary>
    /// Residual likelihood (after controls). Optional — risks may not have
    /// residual values until controls have been documented.
    /// </summary>
    [Range(1, 5)]
    public int? ResidualLikelihood { get; set; }

    /// <summary>
    /// Residual impact (after controls). Optional.
    /// </summary>
    [Range(1, 5)]
    public int? ResidualImpact { get; set; }

    /// <summary>
    /// Residual risk score (ResidualLikelihood × ResidualImpact). DATA-002.
    /// </summary>
    [Range(0, 25)]
    public int? ResidualRiskScore { get; set; }

    /// <summary>
    /// Risk level (calculated from risk score)
    /// </summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;

    /// <summary>
    /// Risk appetite/tolerance level
    /// </summary>
    public RiskLevel? ToleranceLevel { get; set; }

    /// <summary>
    /// Current controls in place
    /// </summary>
    public string? CurrentControls { get; set; }

    /// <summary>
    /// Control effectiveness (1-5, 5=Very Effective). DATA-002 — server-side
    /// validation enforced (UI dropdown alone is not sufficient on direct POST).
    /// </summary>
    [Range(1, 5)]
    public int? ControlEffectiveness { get; set; }

    /// <summary>
    /// Risk response strategy
    /// </summary>
    public string? ResponseStrategy { get; set; }

    /// <summary>
    /// Last review date
    /// </summary>
    public DateTime? LastReviewDate { get; set; }

    /// <summary>
    /// Next review date
    /// </summary>
    public DateTime? NextReviewDate { get; set; }

    /// <summary>
    /// Is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public RiskCategory? Category { get; set; }
    public Process? Process { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? Owner { get; set; }
    public ICollection<RiskActionPlan> ActionPlans { get; set; } = new List<RiskActionPlan>();

    // Relationship collections
    public ICollection<AssetRisk> AssetRisks { get; set; } = new List<AssetRisk>();
    public ICollection<ServiceRisk> ServiceRisks { get; set; } = new List<ServiceRisk>();
    public ICollection<ChangeRequestRisk> ChangeRequestRisks { get; set; } = new List<ChangeRequestRisk>();
    public ICollection<Improvement.ImprovementRisk> ImprovementRisks { get; set; } = new List<Improvement.ImprovementRisk>();

    /// <summary>
    /// Calculate inherent risk score
    /// </summary>
    public void CalculateInherentRiskScore()
    {
        InherentRiskScore = Likelihood * Impact;
        RiskLevel = InherentRiskScore switch
        {
            >= 15 => RiskLevel.Critical,
            >= 10 => RiskLevel.High,
            >= 5 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }

    /// <summary>
    /// Calculate residual risk score
    /// </summary>
    public void CalculateResidualRiskScore()
    {
        if (ResidualLikelihood.HasValue && ResidualImpact.HasValue)
        {
            ResidualRiskScore = ResidualLikelihood.Value * ResidualImpact.Value;
        }
    }

    /// <summary>
    /// Check if risk is outside tolerance
    /// </summary>
    public bool IsOutsideTolerance()
    {
        if (!ToleranceLevel.HasValue) return false;
        
        return RiskLevel > ToleranceLevel.Value;
    }

    /// <summary>
    /// Check if review is due
    /// </summary>
    public bool IsReviewDue()
    {
        return NextReviewDate.HasValue && NextReviewDate.Value <= DateTime.UtcNow;
    }
}

