using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Services;

namespace ESEMS.Web.Models.ServiceManagement;

/// <summary>
/// Junction table for many-to-many relationship between Process and Service
/// A process can use multiple services, and a service can support multiple processes
/// </summary>
public class ProcessService
{
    /// <summary>
    /// Process ID
    /// </summary>
    public string ProcessId { get; set; } = string.Empty;

    /// <summary>
    /// Service ID
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>
    /// Criticality of this service to the process (1-5, where 5 is critical)
    /// </summary>
    public int Criticality { get; set; } = 3;

    /// <summary>
    /// Whether this service is mandatory for the process
    /// </summary>
    public bool IsMandatory { get; set; } = true;

    /// <summary>
    /// Additional notes about this process-service relationship
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// When this relationship was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this relationship was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created this relationship
    /// </summary>
    public string? CreatedById { get; set; }

    /// <summary>
    /// User who last updated this relationship
    /// </summary>
    public string? UpdatedById { get; set; }

    /// <summary>
    /// Whether this relationship is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Process? Process { get; set; }
    public Service? Service { get; set; }
}

