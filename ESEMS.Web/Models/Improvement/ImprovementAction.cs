using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Action item within an improvement initiative
/// </summary>
public class ImprovementAction : AuditableBilingualEntity
{
    /// <summary>
    /// Action code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Parent improvement initiative ID
    /// </summary>
    public string ImprovementId { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Current lifecycle status. Type-safe enum (audit #11); persisted as
    /// nvarchar via a value converter in ApplicationDbContext so existing
    /// string rows keep round-tripping unchanged.
    /// </summary>
    public ImprovementActionStatus Status { get; set; } = ImprovementActionStatus.Pending;

    /// <summary>
    /// Priority (1=Highest, 5=Lowest)
    /// </summary>
    [Range(1, 5)]
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Due date
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Completion date
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Assigned user ID
    /// </summary>
    public string? AssignedToId { get; set; }

    /// <summary>
    /// Completion percentage (0-100)
    /// </summary>
    public int CompletionPercentage { get; set; } = 0;

    /// <summary>
    /// Notes/comments
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public ImprovementInitiative? Improvement { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? AssignedTo { get; set; }
}

