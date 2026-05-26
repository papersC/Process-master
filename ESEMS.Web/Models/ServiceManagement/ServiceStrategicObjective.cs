using ESEMS.Web.Models.Services;

namespace ESEMS.Web.Models.ServiceManagement;

/// <summary>
/// Junction table for many-to-many relationship between Service and StrategicObjective
/// A service can be linked to multiple strategic objectives, and an objective can have multiple services
/// </summary>
public class ServiceStrategicObjective
{
    /// <summary>
    /// Service ID
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>
    /// Strategic Objective ID
    /// </summary>
    public string StrategicObjectiveId { get; set; } = string.Empty;

    /// <summary>
    /// When this link was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who created this link
    /// </summary>
    public string? CreatedById { get; set; }

    /// <summary>
    /// Whether this link is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Service? Service { get; set; }
    public StrategicObjective? StrategicObjective { get; set; }
}

