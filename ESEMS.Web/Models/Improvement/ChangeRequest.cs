using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Change request entity for process/service modifications
/// </summary>
public class ChangeRequest : AuditableBilingualEntity, IOwnedByUnit
{
    /// <summary>
    /// Change request code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Linked process ID
    /// </summary>
    public string? ProcessId { get; set; }

    /// <summary>
    /// Linked service ID
    /// </summary>
    public string? ServiceId { get; set; }

    /// <summary>
    /// Sourcing improvement initiative (I11 audit finding). MBRGEA §3.2
    /// Change Continuity requires bidirectional traceability — when a CR
    /// originates from an Improvement (Source = InternalImprovement), the
    /// initiative should be navigable from the CR Details page and the CR
    /// should appear on the Improvement Details "Linked Change Requests"
    /// section. Nullable so externally-sourced CRs (regulator, audit
    /// finding, vendor request) remain creatable standalone.
    /// </summary>
    public string? ImprovementId { get; set; }

    /// <summary>
    /// Current status
    /// </summary>
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Submitted;

    /// <summary>
    /// Source of the change request
    /// </summary>
    public ChangeRequestSource Source { get; set; } = ChangeRequestSource.InternalImprovement;

    /// <summary>
    /// External reference ID (e.g., from Innovation Management System)
    /// </summary>
    public string? ExternalReferenceId { get; set; }

    /// <summary>
    /// Priority (1=Highest, 5=Lowest)
    /// </summary>
    [Range(1, 5)]
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Justification for the change
    /// </summary>
    public string? Justification { get; set; }

    /// <summary>
    /// Impact assessment
    /// </summary>
    public string? ImpactAssessment { get; set; }

    /// <summary>
    /// Requested by (User ID)
    /// </summary>
    public string? RequestedById { get; set; }

    /// <summary>Reviewer who picked up the CR (transition Submitted → UnderReview).</summary>
    public string? ReviewStartedById { get; set; }

    /// <summary>Timestamp when the CR moved to UnderReview.</summary>
    public DateTime? ReviewStartedAt { get; set; }

    /// <summary>
    /// Approved by (User ID)
    /// </summary>
    public string? ApprovedById { get; set; }

    /// <summary>
    /// Approval date
    /// </summary>
    public DateTime? ApprovalDate { get; set; }

    /// <summary>User who marked the CR as Implemented.</summary>
    public string? ImplementedById { get; set; }

    /// <summary>
    /// Implementation date (set when the change is marked Implemented).
    /// </summary>
    public DateTime? ImplementationDate { get; set; }

    /// <summary>
    /// Rejection reason (if rejected)
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>User who cancelled the CR.</summary>
    public string? CancelledById { get; set; }

    /// <summary>Timestamp when the CR was cancelled.</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>Free-text reason for cancellation (capped 1000 chars).</summary>
    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Owning organizational unit ID
    /// </summary>
    public int? OwningUnitId { get; set; }

    // Navigation properties
    public Process? Process { get; set; }
    public Services.Service? Service { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? RequestedBy { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? ApprovedBy { get; set; }
    public OrganizationUnit? OwningUnit { get; set; }
    public ICollection<ChangeRequestComment> Comments { get; set; } = new List<ChangeRequestComment>();

    // Relationship collections
    public ICollection<ChangeRequestAsset> ChangeRequestAssets { get; set; } = new List<ChangeRequestAsset>();
    public ICollection<ChangeRequestRisk> ChangeRequestRisks { get; set; } = new List<ChangeRequestRisk>();
}

/// <summary>
/// Comment on a change request
/// </summary>
public class ChangeRequestComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ChangeRequestId { get; set; } = string.Empty;
    [Required]
    [MaxLength(4000)]
    public string Comment { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ChangeRequest? ChangeRequest { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? User { get; set; }
}

