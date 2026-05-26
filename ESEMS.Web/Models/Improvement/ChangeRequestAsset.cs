using ESEMS.Web.Models.AssetManagement;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Represents the relationship between a Change Request and an Asset
/// Links change requests to the assets they affect
/// </summary>
public class ChangeRequestAsset
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Change Request ID
    /// </summary>
    public string ChangeRequestId { get; set; } = string.Empty;

    /// <summary>
    /// Asset ID
    /// </summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Type of impact (Modify, Replace, Retire, Add)
    /// </summary>
    public string ImpactType { get; set; } = "Modify";

    /// <summary>
    /// Description of how this change affects the asset
    /// </summary>
    public string? ImpactDescription { get; set; }

    /// <summary>
    /// Whether this asset is critical to the change
    /// </summary>
    public bool IsCritical { get; set; } = false;

    // Audit properties
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    // Navigation properties
    public ChangeRequest? ChangeRequest { get; set; }
    public Asset? Asset { get; set; }
}

