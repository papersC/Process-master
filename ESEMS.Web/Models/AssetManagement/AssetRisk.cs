using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.RiskManagement;

namespace ESEMS.Web.Models.AssetManagement;

/// <summary>
/// Represents the relationship between an Asset and a Risk
/// Links assets to the risks that threaten them
/// </summary>
public class AssetRisk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Asset ID
    /// </summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Enterprise Risk ID
    /// </summary>
    public string RiskId { get; set; } = string.Empty;

    /// <summary>
    /// Impact level if this risk materializes for this specific asset (1-5)
    /// </summary>
    [Range(1, 5)]
    public int ImpactLevel { get; set; } = 3;

    /// <summary>
    /// Specific controls in place to protect this asset from this risk
    /// </summary>
    public string? SpecificControls { get; set; }

    /// <summary>
    /// Whether this asset-risk relationship is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Notes about this specific asset-risk relationship
    /// </summary>
    public string? Notes { get; set; }

    // Audit properties
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    // Navigation properties
    public Asset? Asset { get; set; }
    public EnterpriseRisk? Risk { get; set; }
}

