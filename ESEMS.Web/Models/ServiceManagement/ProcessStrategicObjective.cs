using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Services;

namespace ESEMS.Web.Models.ServiceManagement;

/// <summary>
/// Junction table for many-to-many relationship between Process and StrategicObjective
/// A process can be linked to multiple strategic objectives, and an objective can have multiple processes
/// </summary>
public class ProcessStrategicObjective
{
    /// <summary>
    /// Process ID
    /// </summary>
    public string ProcessId { get; set; } = string.Empty;

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
    public Process? Process { get; set; }
    public StrategicObjective? StrategicObjective { get; set; }
}

