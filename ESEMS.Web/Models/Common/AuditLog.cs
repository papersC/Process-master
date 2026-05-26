using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.Common;

/// <summary>
/// Audit log entry for tracking all system changes
/// </summary>
public class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID who performed the action
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Username for display
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Type of action performed
    /// </summary>
    public AuditAction Action { get; set; }

    /// <summary>
    /// Entity type (e.g., "Process", "Service", "Category")
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Entity ID
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Entity name for display
    /// </summary>
    public string? EntityName { get; set; }

    /// <summary>
    /// Old values (JSON serialized)
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// New values (JSON serialized)
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Changed properties (comma-separated)
    /// </summary>
    public string? ChangedProperties { get; set; }

    /// <summary>
    /// IP address of the user
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Timestamp of the action
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation property
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? User { get; set; }

    /// <summary>
    /// Gets the list of changed properties
    /// </summary>
    public List<string> GetChangedPropertyList()
    {
        if (string.IsNullOrEmpty(ChangedProperties))
            return new List<string>();
        return ChangedProperties.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim())
                                .ToList();
    }
}

