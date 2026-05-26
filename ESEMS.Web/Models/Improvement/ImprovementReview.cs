using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Stage-gate review record for an <see cref="ImprovementInitiative"/>.
///
/// Stage gates are a DGEP / 4Gen Excellence expectation: every live
/// initiative must go through a quarterly (or mid-point) health check
/// where a reviewer rates it Green / Amber / Red, notes the current
/// risks, and sets the next review date.
///
/// Each review is immutable once filed — you don't edit a past review,
/// you file a new one. The initiative's "current health" is simply the
/// most recent review.
/// </summary>
public class ImprovementReview
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string ImprovementId { get; set; } = string.Empty;

    /// <summary>
    /// When this review was held.
    /// </summary>
    public DateTime ReviewDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Health status at the time of review. "Green" = on track,
    /// "Amber" = at risk, "Red" = off track. Stored as a string so
    /// new values (e.g., "Blocked") can be added without a migration.
    /// </summary>
    [Required, MaxLength(20)]
    public string HealthStatus { get; set; } = "Green";

    /// <summary>
    /// Bilingual reviewer notes (free-form). Typical content:
    /// milestones delivered this period, blockers, changes to scope.
    /// </summary>
    public string? NotesEn { get; set; }
    public string? NotesAr { get; set; }

    /// <summary>
    /// Optional reference to an updated progress percentage the
    /// reviewer recommends. When set, the parent initiative's
    /// ProgressPercentage is updated to match on save.
    /// </summary>
    public int? ProgressPercentageSnapshot { get; set; }

    /// <summary>
    /// When the next review should happen. Used by the reminder
    /// background service to notify the reviewer when it's due.
    /// </summary>
    public DateTime? NextReviewDate { get; set; }

    /// <summary>
    /// UserId of the reviewer.
    /// </summary>
    public int? ReviewedById { get; set; }

    [MaxLength(200)]
    public string? ReviewedByName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ImprovementId))]
    public ImprovementInitiative? Improvement { get; set; }
}
