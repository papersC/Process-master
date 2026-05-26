using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Team member assignment for improvement initiatives with specific lifecycle roles
/// </summary>
public class ImprovementTeamMember
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>
    /// Parent improvement initiative ID
    /// </summary>
    public string ImprovementId { get; set; } = string.Empty;

    /// <summary>
    /// Assigned user ID. Real FK to <see cref="CustomUser.UserId"/> (audit #14).
    /// Was previously an unenforced string; now type-safe int so EF prevents
    /// orphan rows when a user is deleted/deactivated.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Role in the improvement lifecycle
    /// </summary>
    public ImprovementLifecycleRole Role { get; set; }

    /// <summary>
    /// Whether this team member is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Additional notes about this assignment
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ImprovementInitiative? Improvement { get; set; }
    public CustomUser? User { get; set; }
}

