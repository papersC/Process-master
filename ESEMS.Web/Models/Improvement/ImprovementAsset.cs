using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.AssetManagement;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Many-to-many junction between <see cref="ImprovementInitiative"/> and
/// <see cref="Asset"/> (audit #7). Closes the gap that used to make it
/// impossible to query "every initiative affecting Asset X" — required for
/// portfolio analytics, depreciation impact tracking, and DGEP 4G Asset
/// Management traceability.
///
/// The relationship is qualified by <see cref="RelationshipType"/> so the
/// portfolio view can distinguish "Replaces" (asset retirement) from
/// "Upgrades" (asset modification) from "Adds" (new asset acquisition).
/// </summary>
public enum AssetRelationshipType
{
    /// <summary>Initiative will replace this asset entirely (retirement).</summary>
    Replaces,
    /// <summary>Initiative will retire / decommission this asset.</summary>
    Retires,
    /// <summary>Initiative modifies / upgrades this asset.</summary>
    Upgrades,
    /// <summary>Initiative adds a new asset (procurement-driven).</summary>
    Adds,
    /// <summary>Initiative has a less-specific relationship to this asset.</summary>
    Other
}

public class ImprovementAsset
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string ImprovementId { get; set; } = string.Empty;

    [Required]
    public string AssetId { get; set; } = string.Empty;

    public AssetRelationshipType RelationshipType { get; set; } = AssetRelationshipType.Other;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }

    // Navigation
    public ImprovementInitiative? Improvement { get; set; }
    public Asset? Asset { get; set; }
}
