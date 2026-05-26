using ESEMS.Web.Models.AssetManagement;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// Represents the relationship between a Service and an Asset
/// Links services to the assets they depend on
/// </summary>
public class ServiceAsset
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Service ID
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>
    /// Asset ID
    /// </summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Criticality of this asset to the service (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    public int Criticality { get; set; } = 3;

    /// <summary>
    /// Whether this asset is required for the service to function
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Usage description - how this asset is used by the service
    /// </summary>
    public string? UsageDescription { get; set; }

    /// <summary>
    /// Whether this relationship is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Audit properties
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    // Navigation properties
    public Service? Service { get; set; }
    public Asset? Asset { get; set; }
}

