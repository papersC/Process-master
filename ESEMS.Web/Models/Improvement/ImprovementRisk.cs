using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.RiskManagement;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Junction entity linking an Improvement Initiative to an Enterprise Risk.
///
/// Mirrors the pattern from the PManagement project (InitiativeRiskLink):
/// an improvement can either introduce risk (e.g. change with adoption risk)
/// or mitigate/reduce an existing risk. The <see cref="RelationshipType"/>
/// captures this semantic so the heat-map and dashboards can distinguish
/// "risks this initiative creates" vs. "risks this initiative addresses".
/// </summary>
public class ImprovementRisk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Improvement initiative ID (FK)</summary>
    public string ImprovementId { get; set; } = string.Empty;

    /// <summary>Enterprise Risk ID (FK)</summary>
    public string RiskId { get; set; } = string.Empty;

    /// <summary>
    /// How this improvement relates to the risk.
    /// One of: "Mitigates", "Creates", "Increases", "Decreases" (default: "Mitigates").
    /// </summary>
    [StringLength(50)]
    public string RelationshipType { get; set; } = "Mitigates";

    /// <summary>
    /// Expected percentage risk reduction this initiative will deliver
    /// if the relationship is "Mitigates" or "Decreases" (0-100).
    /// </summary>
    [Range(0, 100)]
    public int? ExpectedRiskReduction { get; set; }

    /// <summary>Free-text explanation of the link.</summary>
    [StringLength(1000)]
    public string? ImpactDescription { get; set; }

    /// <summary>Notes about this specific improvement-risk relationship.</summary>
    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Whether this link is currently active.</summary>
    public bool IsActive { get; set; } = true;

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    // Navigation
    public ImprovementInitiative? Improvement { get; set; }
    public EnterpriseRisk? Risk { get; set; }
}
