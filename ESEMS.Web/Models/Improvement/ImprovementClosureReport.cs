using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Formal closure record for an <see cref="ImprovementInitiative"/>.
/// Produced when an owner clicks the "Close Initiative" button from the
/// Details page. Captures the final post-implementation data that the
/// ongoing fields (ActualCostSavings / ActualTimeSavings / CompletedDate
/// on ImprovementInitiative itself) don't represent well:
///
/// 1. Bilingual lessons learned (what worked / what didn't)
/// 2. Sign-off metadata (who signed it off, when, whether they're the
///    process owner acting in that capacity)
/// 3. Free-form closing comments
///
/// An initiative can only have ONE closure report — enforced by a
/// unique index on ImprovementId. Re-closing an already-closed
/// initiative updates the existing row instead of creating a duplicate.
/// </summary>
public class ImprovementClosureReport
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string ImprovementId { get; set; } = string.Empty;

    /// <summary>
    /// Bilingual lessons learned. Both may be optional to keep the form
    /// friction low — the governance team has said they prefer a note
    /// in at least one language over a blocked closure.
    /// </summary>
    public string? LessonsLearnedEn { get; set; }
    public string? LessonsLearnedAr { get; set; }

    /// <summary>
    /// Free-form closing comments (e.g. "escalated to Phase 2 under the
    /// Digital Transformation programme"). Separate from LessonsLearned
    /// because reviewers have historically conflated the two.
    /// </summary>
    public string? ClosingComments { get; set; }

    /// <summary>
    /// UserId of the person who signed off on the closure. Typically
    /// the Process Owner, but any user with CanApprove works.
    /// </summary>
    public int? SignedOffById { get; set; }
    public string? SignedOffByName { get; set; }
    public DateTime? SignedOffAt { get; set; }

    /// <summary>
    /// UserId of whoever actually clicked "Close". May differ from the
    /// sign-off user when the Owner files the report and the Process
    /// Owner signs it off afterwards.
    /// </summary>
    public int? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public DateTime ClosedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ImprovementId))]
    public ImprovementInitiative? Improvement { get; set; }
}
