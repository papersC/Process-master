using ESEMS.Web.Models.RiskManagement;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Represents the relationship between a Change Request and a Risk
/// Links change requests to risks they create, mitigate, or affect
/// </summary>
public class ChangeRequestRisk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Change Request ID
    /// </summary>
    public string ChangeRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Enterprise Risk ID
    /// </summary>
    public string RiskId { get; set; } = string.Empty;

    /// <summary>
    /// Type of relationship (Creates, Mitigates, Increases, Decreases)
    /// </summary>
    public string RelationshipType { get; set; } = "Affects";

    /// <summary>
    /// Description of how this change affects the risk
    /// </summary>
    public string? ImpactDescription { get; set; }

    /// <summary>
    /// Expected change in risk level after implementation
    /// </summary>
    public string? ExpectedRiskChange { get; set; }

    // Audit properties
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    // Navigation properties
    public ChangeRequest? ChangeRequest { get; set; }
    public EnterpriseRisk? Risk { get; set; }
}

